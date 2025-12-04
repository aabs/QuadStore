using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using Xunit.Abstractions;
using Roaring.Net.CRoaring;
using Xunit;

namespace TripleStore.Tests;

public class IntersectionBenchmarks
{
    private readonly ITestOutputHelper _output;
    public IntersectionBenchmarks(ITestOutputHelper output) { _output = output; }
    private static Roaring32Bitmap MakeBitmap(int count, int step, int offset = 0)
    {
        var bm = new Roaring32Bitmap();
        for (int i = 0; i < count; i++)
        {
            bm.Add((uint)(offset + i * step));
        }
        return bm;
    }

    private static long MeasureMs(Action action, int iterations = 1)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++) action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    [Theory]
    [InlineData(100_000, 1, 0)]
    [InlineData(1_000_000, 1, 0)]
    [InlineData(1_000_000, 2, 0)]
    public void Compare_HashSet_vs_TwoPointer(int size, int step, int offset)
    {
        var a = MakeBitmap(size, step, offset);
        var b = MakeBitmap(size, step, offset + step); // shifted to create 50% overlap when step==1

        // Materialize arrays (ordered)
        var arrA = a.ToArray();
        var arrB = b.ToArray();

        // Sanity: two-pointer and HashSet produce identical results
        var hs = new HashSet<uint>(arrA);
        hs.IntersectWith(arrB);
        var hsRes = hs.OrderBy(x => x).ToArray();

        var tpRes = TwoPointerIntersect(arrA, arrB);
        tpRes.Should().BeEquivalentTo(hsRes);

        // Warmups
        _ = TwoPointerIntersect(arrA, arrB);
        var hsWarm = new HashSet<uint>(arrA);
        hsWarm.IntersectWith(arrB);

        // Measure
        var hsMs = MeasureMs(() =>
        {
            var set = new HashSet<uint>(arrA);
            set.IntersectWith(arrB);
        }, iterations: 5);

        var tpMs = MeasureMs(() =>
        {
            _ = TwoPointerIntersect(arrA, arrB);
        }, iterations: 5);

        // Output numbers via Assert message
        // Note: xUnit doesn't have TestContext; include results in assertion message
        _output.WriteLine($"Size={size}, step={step}: HashSet={hsMs}ms, TwoPtr={tpMs}ms");
        Assert.True(tpMs <= hsMs * 2, $"Size={size}, step={step}: HashSet={hsMs}ms, TwoPtr={tpMs}ms");
        // Basic expectation already asserted above
    }

    private static uint[] TwoPointerIntersect(uint[] a, uint[] b)
    {
        var tmp = new List<uint>(Math.Min(a.Length, b.Length));
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            var va = a[i];
            var vb = b[j];
            if (va == vb)
            {
                tmp.Add(va);
                i++; j++;
            }
            else if (va < vb) i++; else j++;
        }
        return tmp.ToArray();
    }
}
