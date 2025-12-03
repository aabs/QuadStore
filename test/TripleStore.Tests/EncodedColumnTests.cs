using System;
using System.IO;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class EncodedColumnTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_col_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Test_AppendAndRead_SingleValue()
    {
        var dir = NewTempDir();
        var col = new EncodedColumn(Path.Combine(dir, "column.bin"), 16);
        col.Open();
        col.Append(42);
        col.Read(0).Should().Be(42);
    }

    [Fact]
    public void Test_AppendMultipleValues_RowCountMatches()
    {
        var dir = NewTempDir();
        var col = new EncodedColumn(Path.Combine(dir, "column.bin"), 16);
        col.Open();
        for (int i = 0; i < 100; i++) col.Append(i);
        col.Length.Should().Be(100);
    }

    [Fact]
    public void Test_PersistAndReload_ColumnIntegrity()
    {
        var dir = NewTempDir();
        var path = Path.Combine(dir, "column.bin");
        var col = new EncodedColumn(path, 16);
        col.Open();
        for (int i = 0; i < 128; i++) col.Append(i * 2);
        col.Flush();

        var col2 = new EncodedColumn(path, 16);
        col2.Open();
        col2.Length.Should().Be(128);
        for (int i = 0; i < 128; i++) col2.Read(i).Should().Be(i * 2);
    }
}
