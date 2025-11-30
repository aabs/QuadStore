using System;
using System.Collections.Generic;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class ItemRegistryTests
{
    [Fact]
    public void CanCreateRegistry()
    {
        var sut = new ItemRegistry<string>();
        sut.Should().NotBeNull();
    }

    [Fact]
    public void Add_ReturnsIncrementingIds()
    {
        var sut = new ItemRegistry<string>();
        var id1 = sut.Add("a");
        var id2 = sut.Add("b");

        id1.Should().Be(0);
        id2.Should().Be(1);
    }

    [Fact]
    public void Add_SameItem_ReturnsSameId()
    {
        var sut = new ItemRegistry<string>();
        var first = sut.Add("value");
        var second = sut.Add("value");

        second.Should().Be(first);
    }

    [Fact]
    public void Lookup_ReturnsStoredItem()
    {
        var sut = new ItemRegistry<string>();
        var id = sut.Add("hello");

        var value = sut.Lookup(id);
        value.Should().Be("hello");
    }

    [Fact]
    public void Get_ReturnsIdForExistingItem()
    {
        var sut = new ItemRegistry<string>();
        var id = sut.Add("alpha");

        var lookedUpId = sut.Get("alpha");
        lookedUpId.Should().Be(id);
    }

    [Fact]
    public void Get_UnknownItem_Throws()
    {
        var sut = new ItemRegistry<string>();
        var action = () => sut.Get("missing");
        action.Should().Throw<ApplicationException>()
            .WithMessage("not recognised");
    }

    [Fact]
    public void Lookup_UnknownId_Throws()
    {
        var sut = new ItemRegistry<string>();
        Action action = () => sut.Lookup(123);

        action.Should().Throw<KeyNotFoundException>();
    }

    private sealed class CollisionItem
    {
        private readonly int _hash;
        public CollisionItem(int hash, string value) { _hash = hash; Value = value; }
        public string Value { get; }
        public override int GetHashCode() => _hash;
        public override string ToString() => Value;
    }

    [Fact]
    public void ItemsWithSameHashCode_MapToSameId()
    {
        var sut = new ItemRegistry<CollisionItem>();
        var a = new CollisionItem(42, "A");
        var b = new CollisionItem(42, "B");

        var idA = sut.Add(a);
        var idB = sut.Add(b);

        idB.Should().Be(idA);
        sut.Lookup(idA).Should().BeSameAs(a);
    }
}
