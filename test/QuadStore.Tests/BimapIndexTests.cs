using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class BitmapIndexTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_idx_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Test_OnAppendAndLookup_SingleValue()
    {
        var dir = NewTempDir();
        var idx = new BitmapIndex(Path.Combine(dir, "index.bin"));
        idx.Add(1, 0);
        idx.GetRows(1).Should().Contain(0);
    }

    [Fact]
    public void Test_OnAppendMultipleValues_LookupContainsAll()
    {
        var dir = NewTempDir();
        var idx = new BitmapIndex(Path.Combine(dir, "index.bin"));
        for (int i = 0; i < 100; i++) idx.Add(1, i);
        idx.GetRows(1).Count().Should().Be(100);
    }

    [Fact]
    public void Remove_ExistingRowId_RemovesItFromBitmap()
    {
        var dir = NewTempDir();
        var idx = new BitmapIndex(Path.Combine(dir, "index.bin"));
        idx.Add(1, 10);
        idx.Add(1, 20);
        idx.Add(1, 30);

        idx.Remove(1, 20);

        idx.GetRows(1).Should().BeEquivalentTo(new long[] { 10, 30 });
    }

    [Fact]
    public void Remove_NonExistentRowId_IsNoOp()
    {
        var dir = NewTempDir();
        var idx = new BitmapIndex(Path.Combine(dir, "index.bin"));
        idx.Add(1, 10);
        idx.Add(1, 20);

        idx.Remove(1, 999);

        idx.GetRows(1).Should().BeEquivalentTo(new long[] { 10, 20 });
    }

    [Fact]
    public void Remove_NonExistentDictionaryId_IsNoOp()
    {
        var dir = NewTempDir();
        var idx = new BitmapIndex(Path.Combine(dir, "index.bin"));
        idx.Add(1, 10);

        var act = () => idx.Remove(999, 10);

        act.Should().NotThrow();
        idx.GetRows(1).Should().BeEquivalentTo(new long[] { 10 });
    }

    [Fact]
    public void Test_PersistAndReload_BitmapIntegrity()
    {
        var dir = NewTempDir();
        var path = Path.Combine(dir, "index.bin");
        var idx = new BitmapIndex(path);
        for (int i = 0; i < 128; i++) idx.Add(1, i);
        for (int i = 0; i < 64; i++) idx.Add(2, i * 2);
        idx.Save();

        var idx2 = new BitmapIndex(path);
        idx2.Load();
        idx2.GetRows(1).SequenceEqual(Enumerable.Range(0, 128).Select(i => (long)i)).Should().BeTrue();
        idx2.GetRows(2).SequenceEqual(Enumerable.Range(0, 64).Select(i => (long)(i * 2))).Should().BeTrue();
    }
}
