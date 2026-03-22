using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

/// <summary>
/// Concurrency stress tests for QuadStore delete and query operations.
/// Validates Requirements 10.1, 10.2, 10.3.
/// </summary>
public class ConcurrencyStressTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_conc_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Concurrent deletes and queries maintain data consistency.
    /// Multiple threads query while another thread deletes.
    /// No exceptions should be thrown, and each query result must be
    /// internally consistent — every returned quad must actually exist
    /// (not be a partially-deleted phantom).
    /// Validates: Requirements 10.1, 10.2, 10.3
    /// </summary>
    [Fact]
    public async Task ConcurrentDeletesAndQueries_MaintainDataConsistency()
    {
        const int totalQuads = 500;
        const int queryThreads = 8;
        const int iterations = 50;

        var dir = NewTempDir();
        var qs = new QuadStore(dir);

        // Seed the store with quads across two predicates
        for (var i = 0; i < totalQuads; i++)
        {
            qs.Append($"ex:S{i}", "ex:type", "ex:Person", "ex:G1");
        }

        var exceptions = new ConcurrentBag<Exception>();
        var cts = new CancellationTokenSource();

        // Writer task: delete quads one at a time
        var deleteTask = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < totalQuads; i++)
                {
                    if (cts.Token.IsCancellationRequested) break;
                    qs.Delete(subject: $"ex:S{i}");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Reader tasks: continuously query and validate results
        var readerTasks = Enumerable.Range(0, queryThreads).Select(_ => Task.Run(() =>
        {
            try
            {
                for (var iter = 0; iter < iterations; iter++)
                {
                    if (cts.Token.IsCancellationRequested) break;

                    var results = qs.Query(predicate: "ex:type").ToList();

                    // Each result must be a valid quad — no nulls or partial data
                    foreach (var (s, p, o, g) in results)
                    {
                        s.Should().StartWith("ex:S");
                        p.Should().Be("ex:type");
                        o.Should().Be("ex:Person");
                        g.Should().Be("ex:G1");
                    }

                    // Count must be between 0 and totalQuads (monotonically decreasing
                    // as deletes proceed, but we only assert the valid range)
                    results.Count.Should().BeInRange(0, totalQuads);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(new[] { deleteTask }.Concat(readerTasks));
        cts.Cancel();

        exceptions.Should().BeEmpty("no exceptions should occur during concurrent deletes and queries");

        // After all deletes complete, store should be empty
        var remaining = qs.Query().ToList();
        remaining.Should().BeEmpty("all quads should have been deleted");
    }

    /// <summary>
    /// Concurrent appends and deletes with no lost operations.
    /// Multiple threads append while others delete a known subset.
    /// After completion, the remaining count must equal appended minus deleted.
    /// Validates: Requirements 10.1, 10.2
    /// </summary>
    [Fact]
    public async Task ConcurrentAppendsAndDeletes_NoLostOperations()
    {
        const int appendThreads = 4;
        const int quadsPerThread = 200;
        const int totalAppended = appendThreads * quadsPerThread;

        var dir = NewTempDir();
        var qs = new QuadStore(dir);

        var exceptions = new ConcurrentBag<Exception>();

        // Pre-seed some quads that will be deleted concurrently
        const int preSeeded = 100;
        for (var i = 0; i < preSeeded; i++)
        {
            qs.Append($"ex:Pre{i}", "ex:seed", "ex:Value", "ex:G0");
        }

        // Use a barrier so all threads start at roughly the same time
        using var barrier = new Barrier(appendThreads + 1); // +1 for delete thread

        // Append threads: each appends quads with a unique subject prefix
        var appendTasks = Enumerable.Range(0, appendThreads).Select(t => Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                for (var i = 0; i < quadsPerThread; i++)
                {
                    qs.Append($"ex:T{t}_Q{i}", "ex:appended", "ex:Data", "ex:G1");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        // Delete thread: deletes all pre-seeded quads concurrently with appends
        var deleteTask = Task.Run(() =>
        {
            try
            {
                barrier.SignalAndWait();
                for (var i = 0; i < preSeeded; i++)
                {
                    qs.Delete(subject: $"ex:Pre{i}");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(appendTasks.Concat(new[] { deleteTask }));

        exceptions.Should().BeEmpty("no exceptions should occur during concurrent appends and deletes");

        // All pre-seeded quads should be deleted
        var seedRemaining = qs.Query(predicate: "ex:seed").ToList();
        seedRemaining.Should().BeEmpty("all pre-seeded quads should have been deleted");

        // All appended quads should be present
        var appendedRemaining = qs.Query(predicate: "ex:appended").ToList();
        appendedRemaining.Should().HaveCount(totalAppended,
            "all appended quads should survive — no lost appends");

        // Total store count = only the appended quads
        var total = qs.Query().ToList();
        total.Should().HaveCount(totalAppended);
    }

    /// <summary>
    /// No phantom reads within a single lock acquisition.
    /// A single Query enumeration should see a consistent snapshot —
    /// no quad should appear then disappear mid-enumeration.
    /// We verify this by fully materializing query results and checking
    /// that each result set contains no duplicates and all quads are valid.
    /// Validates: Requirements 10.2, 10.3
    /// </summary>
    [Fact]
    public async Task NoPhantomReads_WithinSingleQueryEnumeration()
    {
        const int totalQuads = 300;
        const int readerIterations = 100;
        const int readerThreads = 4;

        var dir = NewTempDir();
        var qs = new QuadStore(dir);

        // Seed the store
        for (var i = 0; i < totalQuads; i++)
        {
            qs.Append($"ex:S{i}", "ex:rel", "ex:Obj", "ex:G1");
        }

        var exceptions = new ConcurrentBag<Exception>();
        var phantomDetected = new ConcurrentBag<string>();

        // Writer: continuously deletes quads
        var deleteTask = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < totalQuads; i++)
                {
                    qs.Delete(subject: $"ex:S{i}");
                    // Small yield to interleave with readers
                    if (i % 10 == 0) Thread.Yield();
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Readers: each query must return a consistent snapshot
        var readerTasks = Enumerable.Range(0, readerThreads).Select(_ => Task.Run(() =>
        {
            try
            {
                for (var iter = 0; iter < readerIterations; iter++)
                {
                    // Materialize the full result set under a single read lock
                    var results = qs.Query(predicate: "ex:rel").ToList();

                    // Check for duplicates — a phantom could manifest as
                    // seeing the same subject twice or an inconsistent set
                    var subjects = results.Select(r => r.subject).ToList();
                    var distinctSubjects = subjects.Distinct().ToList();

                    if (subjects.Count != distinctSubjects.Count)
                    {
                        phantomDetected.Add(
                            $"Duplicate subjects detected: {subjects.Count} total vs {distinctSubjects.Count} distinct");
                    }

                    // Every returned quad must have consistent field values
                    foreach (var (s, p, o, g) in results)
                    {
                        if (p != "ex:rel" || o != "ex:Obj" || g != "ex:G1")
                        {
                            phantomDetected.Add(
                                $"Inconsistent quad: ({s}, {p}, {o}, {g})");
                        }
                    }

                    // The count must be in valid range
                    results.Count.Should().BeInRange(0, totalQuads);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(new[] { deleteTask }.Concat(readerTasks));

        exceptions.Should().BeEmpty("no exceptions should occur during concurrent operations");
        phantomDetected.Should().BeEmpty("no phantom reads should occur within a single query enumeration");

        // After all deletes, store should be empty
        qs.Query().ToList().Should().BeEmpty();
    }
}
