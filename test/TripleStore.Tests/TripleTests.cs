using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

// Disable parallelization because tests share a global singleton registry
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TripleStore.Tests;

public class TripleTests
{
    [Fact]
    public void ConstructWithUris_ShouldRegisterAndExposeOrdinalsAndUris()
    {
        // Arrange
        var s = new Uri("http://example.org/subject/" + Guid.NewGuid());
        var p = new Uri("http://example.org/predicate/" + Guid.NewGuid());
        var o = new Uri("http://example.org/object/" + Guid.NewGuid());

        // Act
        var triple = new Triple(s, p, o);

        // Assert
        triple.Subject.Should().Be(s);
        triple.Predicate.Should().Be(p);
        triple.Object.Should().Be(o);

        // Ordinals should be retrievable from registry via Get
        var registry = RdfCompressionContext.Instance.UriRegistry;
        registry.Get(s).Should().Be(triple.SubjOrd);
        registry.Get(p).Should().Be(triple.PredOrd);
        registry.Get(o).Should().Be(triple.ObjOrd);
    }

    [Fact]
    public void ConstructWithSameUris_ShouldReuseExistingOrdinals()
    {
        // Arrange
        var s = new Uri("http://example.org/subject/" + Guid.NewGuid());
        var p = new Uri("http://example.org/predicate/" + Guid.NewGuid());
        var o = new Uri("http://example.org/object/" + Guid.NewGuid());
        var first = new Triple(s, p, o);

        // Act
        var second = new Triple(s, p, o);

        // Assert
        second.SubjOrd.Should().Be(first.SubjOrd);
        second.PredOrd.Should().Be(first.PredOrd);
        second.ObjOrd.Should().Be(first.ObjOrd);
        second.Subject.Should().Be(s);
        second.Predicate.Should().Be(p);
        second.Object.Should().Be(o);
    }

    [Fact]
    public void IntConstructorWithExistingRegistryEntries_ShouldReturnUris()
    {
        // Arrange - seed registry using uri ctor
        var s = new Uri("http://example.org/subject/" + Guid.NewGuid());
        var p = new Uri("http://example.org/predicate/" + Guid.NewGuid());
        var o = new Uri("http://example.org/object/" + Guid.NewGuid());
        var seeded = new Triple(s, p, o);

        // Act - construct by ordinals
        var byInts = new Triple(seeded.SubjOrd, seeded.PredOrd, seeded.ObjOrd);

        // Assert
        byInts.Subject.Should().Be(s);
        byInts.Predicate.Should().Be(p);
        byInts.Object.Should().Be(o);
    }

    [Fact]
    public void IntConstructorWithUnknownOrdinals_ShouldThrowOnPropertyAccess()
    {
        // Arrange - choose large values unlikely to exist
        var triple = new Triple(int.MaxValue - 2, int.MaxValue - 1, int.MaxValue);

        // Act / Assert
        Assert.Throws<KeyNotFoundException>(() => { var _ = triple.Subject; });
        Assert.Throws<KeyNotFoundException>(() => { var _ = triple.Predicate; });
        Assert.Throws<KeyNotFoundException>(() => { var _ = triple.Object; });
    }

    [Fact]
    public void IntConstructorWithNegativeOrdinals_ShouldThrowOnPropertyAccess()
    {
        // Arrange
        var triple = new Triple(-42, -43, -44);

        // Act / Assert
        Assert.Throws<KeyNotFoundException>(() => { var _ = triple.Subject; });
        Assert.Throws<KeyNotFoundException>(() => { var _ = triple.Predicate; });
		Assert.Throws<KeyNotFoundException>(() => { var _ = triple.Object; });
    }

    [Fact]
    public void IntConstructorWithPartiallyInvalidOrdinals_ShouldThrowOnlyForInvalid()
    {
        // Arrange create a valid triple
        var s = new Uri("http://example.org/subject/" + Guid.NewGuid());
        var p = new Uri("http://example.org/predicate/" + Guid.NewGuid());
        var o = new Uri("http://example.org/object/" + Guid.NewGuid());
        var valid = new Triple(s, p, o);

        // Use valid subject & object ordinals but invalid predicate ordinal
        var mixed = new Triple(valid.SubjOrd, int.MaxValue, valid.ObjOrd);

        // Act / Assert
        mixed.Subject.Should().Be(s);
        Assert.Throws<KeyNotFoundException>(() => { var _ = mixed.Predicate; });
        mixed.Object.Should().Be(o);
    }

    [Fact]
    public void SettingNullUris_ShouldThrowNullReferenceException()
    {
        // Arrange
        var triple = new Triple(new Uri("http://example.org/s/" + Guid.NewGuid()),
                                new Uri("http://example.org/p/" + Guid.NewGuid()),
                                new Uri("http://example.org/o/" + Guid.NewGuid()));

        // Act / Assert
        Assert.Throws<NullReferenceException>(() => triple.Subject = null!);
        Assert.Throws<NullReferenceException>(() => triple.Predicate = null!);
        Assert.Throws<NullReferenceException>(() => triple.Object = null!);
    }

    [Fact]
    public void Setters_ShouldUpdateOrdinalsAndLookup()
    {
        // Arrange
        var initial = new Triple(new Uri("http://example.org/s/" + Guid.NewGuid()),
                                 new Uri("http://example.org/p/" + Guid.NewGuid()),
                                 new Uri("http://example.org/o/" + Guid.NewGuid()));

        var newS = new Uri("http://example.org/s2/" + Guid.NewGuid());
        var newP = new Uri("http://example.org/p2/" + Guid.NewGuid());
        var newO = new Uri("http://example.org/o2/" + Guid.NewGuid());

        // Act
        initial.Subject = newS;
        initial.Predicate = newP;
        initial.Object = newO;

        // Assert
        initial.Subject.Should().Be(newS);
        initial.Predicate.Should().Be(newP);
        initial.Object.Should().Be(newO);

        var registry = RdfCompressionContext.Instance.UriRegistry;
        registry.Get(newS).Should().Be(initial.SubjOrd);
        registry.Get(newP).Should().Be(initial.PredOrd);
        registry.Get(newO).Should().Be(initial.ObjOrd);
    }

    [Fact]
    public void GetMethod_ShouldReturnTupleOfOrdinals_AndHashCodeShouldMatch()
    {
        // Arrange
        var s = new Uri("http://example.org/subject/" + Guid.NewGuid());
        var p = new Uri("http://example.org/predicate/" + Guid.NewGuid());
        var o = new Uri("http://example.org/object/" + Guid.NewGuid());
        var triple = new Triple(s, p, o);

        // Act
        var (so, po, oo) = triple.Get();
        var expectedHash = HashCode.Combine(so, po, oo);

        // Assert
        so.Should().Be(triple.SubjOrd);
        po.Should().Be(triple.PredOrd);
        oo.Should().Be(triple.ObjOrd);
        triple.GetHashCode().Should().Be(expectedHash);
    }

    // Negative tests for URI constructor
    [Fact]
    public void Ctor_NullSubject_ShouldThrow()
    {
        Assert.Throws<NullReferenceException>(() => new Triple(null!, new Uri("http://example.org/p"), new Uri("http://example.org/o")));
    }

    [Fact]
    public void Ctor_NullPredicate_ShouldThrow()
    {
        Assert.Throws<NullReferenceException>(() => new Triple(new Uri("http://example.org/s"), null!, new Uri("http://example.org/o")));
    }

    [Fact]
    public void Ctor_NullObject_ShouldThrow()
    {
        Assert.Throws<NullReferenceException>(() => new Triple(new Uri("http://example.org/s"), new Uri("http://example.org/p"), null!));
    }

    [Fact]
    public void Ctor_AllNulls_ShouldThrow()
    {
        Assert.Throws<NullReferenceException>(() => new Triple(null!, null!, null!));
    }

    [Fact]
    public void IntCtor_InvalidOrdinals_ShouldNotThrowUntilAccess()
    {
        // Arrange & Act (constructor should not throw)
        var triple = new Triple(int.MaxValue, int.MaxValue - 1, int.MaxValue - 2);

        // Assert deferred failures on property access
        Assert.Throws<KeyNotFoundException>(() => { var _ = triple.Subject; });
        Assert.Throws<KeyNotFoundException>(() => { var _ = triple.Predicate; });
        Assert.Throws<KeyNotFoundException>(() => { var _ = triple.Object; });
    }

    // Contract tests with the URI registry
    [Fact]
    public void Registry_DistinctUris_ShouldReceiveIncreasingOrdinals()
    {
        // Capture baseline by adding one unique triple
        var baseline = new Triple(new Uri("http://example.org/baseline/" + Guid.NewGuid()), new Uri("http://example.org/p/" + Guid.NewGuid()), new Uri("http://example.org/o/" + Guid.NewGuid()));
        var startOrd = baseline.SubjOrd;

        var created = new List<Triple>();
        for (int i = 0; i < 5; i++)
        {
            created.Add(new Triple(new Uri($"http://example.org/seq/{Guid.NewGuid()}"), new Uri($"http://example.org/p/{Guid.NewGuid()}"), new Uri($"http://example.org/o/{Guid.NewGuid()}")));
        }

        // Subject ordinals should be strictly increasing relative to order of creation (registry uses incremental counter)
        var subjectOrdinals = created.Select(t => t.SubjOrd).ToList();
        subjectOrdinals.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        subjectOrdinals.First().Should().BeGreaterThanOrEqualTo(startOrd + 1);
    }

    [Fact]
    public void Registry_ReassignSameUri_ShouldNotChangeOrdinal()
    {
        var uri = new Uri("http://example.org/reuse/" + Guid.NewGuid());
        var triple = new Triple(uri, new Uri("http://example.org/p/" + Guid.NewGuid()), new Uri("http://example.org/o/" + Guid.NewGuid()));
        var originalOrd = triple.SubjOrd;

        // Reassign identical URI
        triple.Subject = uri;
        triple.SubjOrd.Should().Be(originalOrd);
        triple.Subject.Should().Be(uri);
    }

    [Fact]
    public async Task Registry_ConcurrentAddSameUri_ShouldYieldSingleOrdinal()
    {
        var sharedUri = new Uri("http://example.org/concurrent/" + Guid.NewGuid());
        var ordinals = new List<int>();
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            var t = new Triple(sharedUri, new Uri("http://example.org/p/" + Guid.NewGuid()), new Uri("http://example.org/o/" + Guid.NewGuid()));
            lock (ordinals) ordinals.Add(t.SubjOrd);
        }));
        await Task.WhenAll(tasks);
        ordinals.Should().AllBeEquivalentTo(ordinals[0]); // all same
    }

    [Fact]
    public async Task Registry_ConcurrentAddDistinctUris_ShouldProduceDistinctOrdinals()
    {
        var ordinals = new ConcurrentBag<int>();
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            var t = new Triple(new Uri("http://example.org/concurrentDistinct/" + Guid.NewGuid()), new Uri("http://example.org/p/" + Guid.NewGuid()), new Uri("http://example.org/o/" + Guid.NewGuid()));
            ordinals.Add(t.SubjOrd);
        }));
        await Task.WhenAll(tasks);
        ordinals.Should().HaveCount(10);
        ordinals.Distinct().Should().HaveCount(10);
    }
}
