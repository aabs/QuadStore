using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TripleStore.Core;
using Xunit;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace TripleStore.Tests;

public class TripleCollectionTests
{
    [Fact]
    public void InsertTriple_UniqueTriples_ShouldAssignSequentialIndices_AndIncreaseCount()
    {
        var store = new TripleCollection();
        var t1 = new Triple(new Uri("http://example.org/s/" + Guid.NewGuid()), new Uri("http://example.org/p/" + Guid.NewGuid()), new Uri("http://example.org/o/" + Guid.NewGuid()));
        var t2 = new Triple(new Uri("http://example.org/s/" + Guid.NewGuid()), new Uri("http://example.org/p/" + Guid.NewGuid()), new Uri("http://example.org/o/" + Guid.NewGuid()));

        var i1 = store.InsertTriple(t1);
        var i2 = store.InsertTriple(t2);

        i1.Should().Be(0);
        i2.Should().Be(1);
        store.Count.Should().Be(2);

        store.ElementAt(i1).Get().Should().Be(t1.Get());
        store.ElementAt(i2).Get().Should().Be(t2.Get());
    }

    [Fact]
    public void InsertTriple_DuplicateTriple_ShouldReturnExistingIndex_AndNotIncreaseCount()
    {
        var store = new TripleCollection();
        var s = new Uri("http://example.org/s/" + Guid.NewGuid());
        var p = new Uri("http://example.org/p/" + Guid.NewGuid());
        var o = new Uri("http://example.org/o/" + Guid.NewGuid());
        var t = new Triple(s, p, o);

        var first = store.InsertTriple(t);
        var second = store.InsertTriple(new Triple(s, p, o));

        second.Should().Be(first);
        store.Count.Should().Be(1);
        store.ElementAt(first).Get().Should().Be(t.Get());
    }

    [Fact]
    public void Resize_ShouldHandleMoreThanInitialCapacity()
    {
        var store = new TripleCollection();
        int limit = SuperStore.ArraySizeIncrement + 16;
        var subjectBase = "http://example.org/s/";
        var predicateBase = "http://example.org/p/";
        var objectBase = "http://example.org/o/";

        for (int i = 0; i < limit; i++)
        {
            var t = new Triple(new Uri(subjectBase + i), new Uri(predicateBase + i), new Uri(objectBase + i));
            var idx = store.InsertTriple(t);
            idx.Should().Be(i);
        }

        store.Count.Should().Be(limit);

        // spot-check a few entries
        for (int i = 0; i < 5; i++)
        {
            var ords = store.OrdinalsAt(limit - 1 - i);
            var t = new Triple(new Uri(subjectBase + (limit - 1 - i)), new Uri(predicateBase + (limit - 1 - i)), new Uri(objectBase + (limit - 1 - i)));
            ords.Should().Be(t.Get());
        }
    }

    [Fact]
    public void Enumeration_ShouldYieldInsertedTriplesInOrder()
    {
        var store = new TripleCollection();
        var triples = Enumerable.Range(0, 10)
            .Select(i => new Triple(new Uri($"http://example.org/s/{i}"), new Uri($"http://example.org/p/{i}"), new Uri($"http://example.org/o/{i}")))
            .ToList();
        for (int i = 0; i < triples.Count; i++)
        {
            var idx = store.InsertTriple(triples[i]);
            idx.Should().Be(i);
        }

        store.Count.Should().Be(triples.Count);
        for (int i = 0; i < triples.Count; i++)
        {
            store.ElementAt(i).Get().Should().Be(triples[i].Get());
        }
    }

    [Fact]
    public void RandomAccessApi_ShouldResolveUrisViaRegistry()
    {
        var store = new TripleCollection();
        var s = new Uri("http://example.org/s/" + Guid.NewGuid());
        var p = new Uri("http://example.org/p/" + Guid.NewGuid());
        var o = new Uri("http://example.org/o/" + Guid.NewGuid());
        var t = new Triple(s, p, o);
        var idx = store.InsertTriple(t);

        store.SubjectAt(idx).Should().Be(s);
        store.PredicateAt(idx).Should().Be(p);
        store.ObjectAt(idx).Should().Be(o);

        store.SubjectOrdinalAt(idx).Should().Be(t.SubjOrd);
        store.PredicateOrdinalAt(idx).Should().Be(t.PredOrd);
        store.ObjectOrdinalAt(idx).Should().Be(t.ObjOrd);
    }

    [Fact]
    public void Enumerator_MoveNext_Reset_And_Current_Behavior()
    {
        var store = new TripleCollection();
        var triples = Enumerable.Range(0, 3)
            .Select(i => new Triple(new Uri($"http://example.org/s/{i}"), new Uri($"http://example.org/p/{i}"), new Uri($"http://example.org/o/{i}")))
            .ToList();
        for (int i = 0; i < triples.Count; i++) store.InsertTriple(triples[i]);

        // Current before MoveNext should be first element (index 0)
        var en = store.GetEnumerator();
        en.Current.Get().Should().Be(triples[0].Get());

        en.MoveNext().Should().BeTrue(); // move to index 1
        en.Current.Get().Should().Be(triples[1].Get());

        en.MoveNext().Should().BeTrue(); // move to index 2
        en.Current.Get().Should().Be(triples[2].Get());

        en.MoveNext().Should().BeTrue(); // attempt to move past last valid element increments to Count
        en.MoveNext().Should().BeFalse(); // now at end

        en.Reset();
        en.Current.Get().Should().Be(triples[0].Get());
    }

    [Fact]
    public void Resize_Boundary_CapacityLimits()
    {
        var store = new TripleCollection();
        int n = SuperStore.ArraySizeIncrement; // initial capacity per row count

        for (int i = 0; i < n; i++)
        {
            var t = new Triple(new Uri($"http://example.org/s/{i}"), new Uri($"http://example.org/p/{i}"), new Uri($"http://example.org/o/{i}"));
            store.InsertTriple(t).Should().Be(i);
        }
        store.Count.Should().Be(n);

        // Insert one more than twice capacity to force multiple resizes
        int target = n * 2 + 1;
        for (int i = n; i < target; i++)
        {
            var t = new Triple(new Uri($"http://example.org/s/{i}"), new Uri($"http://example.org/p/{i}"), new Uri($"http://example.org/o/{i}"));
            store.InsertTriple(t).Should().Be(i);
        }
        store.Count.Should().Be(target);

        // Validate last entry
        store.ElementAt(target - 1).Get().Should().Be(new Triple(new Uri($"http://example.org/s/{target - 1}"), new Uri($"http://example.org/p/{target - 1}"), new Uri($"http://example.org/o/{target - 1}")).Get());
    }

    [Fact]
    public void InsertTriple_ManyDuplicates_ShouldKeepCountAndStableIndex()
    {
        var store = new TripleCollection();
        var s = new Uri("http://example.org/s/dup");
        var p = new Uri("http://example.org/p/dup");
        var o = new Uri("http://example.org/o/dup");
        var t = new Triple(s, p, o);

        var first = store.InsertTriple(t);
        for (int i = 0; i < 100; i++)
        {
            store.InsertTriple(new Triple(s, p, o)).Should().Be(first);
        }
        store.Count.Should().Be(1);
        store.ElementAt(first).Get().Should().Be(t.Get());
    }

    [Fact]
    public async Task Concurrency_InsertSameTriple_ShouldReturnSingleIndex()
    {
        var store = new TripleCollection();
        var t = new Triple(new Uri("http://example.org/s/concurrent"), new Uri("http://example.org/p/concurrent"), new Uri("http://example.org/o/concurrent"));
        var indices = new ConcurrentBag<int>();

        var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() => indices.Add(store.InsertTriple(t))));
        await Task.WhenAll(tasks);

        indices.Should().HaveCount(32);
        indices.Distinct().Single().Should().Be(0);
        store.Count.Should().Be(1);
    }

    [Fact]
    public async Task Concurrency_InsertDistinctTriples_ShouldAssignDistinctIndices()
    {
        var store = new TripleCollection();
        var indices = new ConcurrentBag<int>();

        var tasks = Enumerable.Range(0, 32).Select(i => Task.Run(() =>
        {
            var ti = new Triple(new Uri($"http://example.org/s/c/{i}"), new Uri($"http://example.org/p/c/{i}"), new Uri($"http://example.org/o/c/{i}"));
            indices.Add(store.InsertTriple(ti));
        }));
        await Task.WhenAll(tasks);

        indices.Should().HaveCount(32);
        indices.Distinct().Should().HaveCount(32);
        store.Count.Should().Be(32);
    }

    [Fact]
    public void RandomAccess_OutOfBounds_ShouldThrow()
    {
        var store = new TripleCollection();
        var idx = store.InsertTriple(new Triple(new Uri("http://example.org/s/ob"), new Uri("http://example.org/p/ob"), new Uri("http://example.org/o/ob")));
        idx.Should().Be(0);

        Assert.Throws<IndexOutOfRangeException>(() => store.ElementAt(-1));
        Assert.Throws<IndexOutOfRangeException>(() => { var _ = store.OrdinalsAt(-1); });
    }
}
