using System;
using System.IO;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class DictionaryEncoderTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_dict_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Test_EncodeAndDecode_SingleValue()
    {
        var dir = NewTempDir();
        var enc = new DictionaryEncoder(Path.Combine(dir, "dictionary.bin"), Path.Combine(dir, "dictionary.tmp"));
        var id = enc.GetOrAdd("ex:Ada");
        enc.GetString(id).Should().Be("ex:Ada");
    }

    [Fact]
    public void Test_Encode_DuplicateValue_ReturnsSameId()
    {
        var dir = NewTempDir();
        var enc = new DictionaryEncoder(Path.Combine(dir, "dictionary.bin"), Path.Combine(dir, "dictionary.tmp"));
        var id1 = enc.GetOrAdd("ex:Ada");
        var id2 = enc.GetOrAdd("ex:Ada");
        id2.Should().Be(id1);
    }

    [Fact]
    public void Test_PersistAndLoad_DictionaryIntegrity()
    {
        var dir = NewTempDir();
        var path = Path.Combine(dir, "dictionary.bin");
        var tmp = Path.Combine(dir, "dictionary.tmp");
        var enc = new DictionaryEncoder(path, tmp);
        var idAda = enc.GetOrAdd("ex:Ada");
        var idBob = enc.GetOrAdd("ex:Bob");
        enc.Save();

        var enc2 = new DictionaryEncoder(path, tmp);
        enc2.Load();
        enc2.GetString(idAda).Should().Be("ex:Ada");
        enc2.GetString(idBob).Should().Be("ex:Bob");
        enc2.TryGet("ex:Ada", out var loadedAda).Should().BeTrue();
        loadedAda.Should().Be(idAda);
    }

    [Fact]
    public void Test_TryGetId_UnknownValue_ReturnsFalse()
    {
        var dir = NewTempDir();
        var enc = new DictionaryEncoder(Path.Combine(dir, "dictionary.bin"), Path.Combine(dir, "dictionary.tmp"));
        enc.TryGet("does-not-exist", out var _).Should().BeFalse();
    }
}
