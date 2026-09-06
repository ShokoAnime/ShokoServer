using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Xunit;

namespace Shoko.Tests.Utilities;

/// <summary>
/// Covers <see cref="PocoCache{TKey, TEntity}"/> and <see cref="PocoIndex{TKey, TEntity, TInverseKey}"/>.
/// Every cached repository in the server keeps its rows in one of these and answers reads from the
/// secondary indexes, so a defect here silently corrupts lookups across the whole application.
/// </summary>
public class PocoCacheTests
{
    private sealed class Item(int id, string category, params string[] tags)
    {
        public int Id { get; set; } = id;

        public string Category { get; set; } = category;

        public IReadOnlyList<string> Tags { get; set; } = tags;
    }

    private static PocoCache<int, Item> Cache(params Item[] items)
        => new(items, i => i.Id);

    #region Cache basics

    [Fact]
    public void Get_ReturnsTheEntityForAKnownKey()
    {
        var item = new Item(1, "a");

        Assert.Same(item, Cache(item).Get(1));
    }

    [Fact]
    public void Get_ReturnsNullForAnUnknownKey()
        => Assert.Null(Cache(new Item(1, "a")).Get(99));

    [Fact]
    public void GetAll_ReturnsEveryEntity()
    {
        var cache = Cache(new Item(1, "a"), new Item(2, "b"));

        Assert.Equal([1, 2], cache.GetAll().Select(i => i.Id).Order());
    }

    [Fact]
    public void GetAllKeys_ReturnsEveryKey()
    {
        var cache = Cache(new Item(1, "a"), new Item(2, "b"));

        Assert.Equal([1, 2], cache.GetAllKeys().Order());
    }

    [Fact]
    public void GetAll_ReturnsASnapshotThatDoesNotTrackLaterWrites()
    {
        var cache = Cache(new Item(1, "a"));

        var snapshot = cache.GetAll();
        cache.Update(new Item(2, "b"));

        Assert.Single(snapshot);
    }

    [Fact]
    public void Constructor_ThrowsWhenTheKeySelectorProducesDuplicates()
        => Assert.Throws<ArgumentException>(() => Cache(new Item(1, "a"), new Item(1, "b")));

    [Fact]
    public void Update_AddsAnEntityThatWasNotPresent()
    {
        var cache = Cache();
        var item = new Item(1, "a");

        cache.Update(item);

        Assert.Same(item, cache.Get(1));
    }

    [Fact]
    public void Update_ReplacesTheEntityStoredUnderTheSameKey()
    {
        var cache = Cache(new Item(1, "a"));
        var replacement = new Item(1, "b");

        cache.Update(replacement);

        Assert.Same(replacement, cache.Get(1));
        Assert.Single(cache.GetAll());
    }

    [Fact]
    public void Remove_DropsTheEntity()
    {
        var item = new Item(1, "a");
        var cache = Cache(item);

        cache.Remove(item);

        Assert.Null(cache.Get(1));
        Assert.Empty(cache.GetAll());
    }

    [Fact]
    public void Clear_DropsEveryEntity()
    {
        var cache = Cache(new Item(1, "a"), new Item(2, "b"));

        cache.Clear();

        Assert.Empty(cache.GetAll());
    }

    #endregion

    #region Single-valued index

    [Fact]
    public void Index_IsPopulatedFromTheEntitiesPresentWhenItIsCreated()
    {
        var cache = Cache(new Item(1, "a"), new Item(2, "b"));

        var index = cache.CreateIndex(i => i.Category);

        Assert.Equal(1, index.GetOne("a")!.Id);
        Assert.Equal(2, index.GetOne("b")!.Id);
    }

    [Fact]
    public void Index_GetOne_ReturnsNullForAnUnknownKey()
        => Assert.Null(Cache(new Item(1, "a")).CreateIndex(i => i.Category).GetOne("zzz"));

    [Fact]
    public void Index_GetMultiple_ReturnsEveryMatch()
    {
        var cache = Cache(new Item(1, "a"), new Item(2, "a"), new Item(3, "b"));
        var index = cache.CreateIndex(i => i.Category);

        Assert.Equal([1, 2], index.GetMultiple("a").Select(i => i.Id).Order());
    }

    [Fact]
    public void Index_GetMultiple_ReturnsEmptyForAnUnknownKey()
        => Assert.Empty(Cache(new Item(1, "a")).CreateIndex(i => i.Category).GetMultiple("zzz"));

    #endregion

    #region Index maintenance

    [Fact]
    public void Index_PicksUpAnEntityAddedAfterTheIndexWasCreated()
    {
        var cache = Cache();
        var index = cache.CreateIndex(i => i.Category);

        cache.Update(new Item(1, "a"));

        Assert.Equal(1, index.GetOne("a")!.Id);
    }

    [Fact]
    public void Index_MovesAnEntityWhenItsIndexedValueChanges()
    {
        var cache = Cache(new Item(1, "a"));
        var index = cache.CreateIndex(i => i.Category);

        cache.Update(new Item(1, "b"));

        // The stale mapping must not survive, or lookups return entities that no longer match.
        Assert.Null(index.GetOne("a"));
        Assert.Equal(1, index.GetOne("b")!.Id);
    }

    [Fact]
    public void Index_DropsAnEntityThatIsRemovedFromTheCache()
    {
        var item = new Item(1, "a");
        var cache = Cache(item);
        var index = cache.CreateIndex(i => i.Category);

        cache.Remove(item);

        Assert.Null(index.GetOne("a"));
        Assert.Empty(index.GetMultiple("a"));
    }

    [Fact]
    public void Index_IsEmptiedWhenTheCacheIsCleared()
    {
        var cache = Cache(new Item(1, "a"), new Item(2, "b"));
        var index = cache.CreateIndex(i => i.Category);

        cache.Clear();

        Assert.Null(index.GetOne("a"));
        Assert.Null(index.GetOne("b"));
    }

    [Fact]
    public void MultipleIndexesOverTheSameCacheAreAllMaintained()
    {
        var cache = Cache(new Item(1, "a"));
        var byCategory = cache.CreateIndex(i => i.Category);
        var byId = cache.CreateIndex(i => i.Id);

        cache.Update(new Item(1, "b"));

        Assert.Null(byCategory.GetOne("a"));
        Assert.Equal(1, byCategory.GetOne("b")!.Id);
        Assert.Equal("b", byId.GetOne(1)!.Category);
    }

    #endregion

    #region Many-valued index

    [Fact]
    public void MultiValuedIndex_IndexesAnEntityUnderEveryValue()
    {
        var cache = Cache(new Item(1, "a", "x", "y"));
        var index = cache.CreateIndex(i => i.Tags);

        Assert.Equal(1, index.GetOne("x")!.Id);
        Assert.Equal(1, index.GetOne("y")!.Id);
    }

    [Fact]
    public void MultiValuedIndex_ReturnsEveryEntitySharingAValue()
    {
        var cache = Cache(new Item(1, "a", "x"), new Item(2, "b", "x", "y"));
        var index = cache.CreateIndex(i => i.Tags);

        Assert.Equal([1, 2], index.GetMultiple("x").Select(i => i.Id).Order());
        Assert.Equal([2], index.GetMultiple("y").Select(i => i.Id));
    }

    [Fact]
    public void MultiValuedIndex_ReplacesTheWholeValueSetOnUpdate()
    {
        var cache = Cache(new Item(1, "a", "x", "y"));
        var index = cache.CreateIndex(i => i.Tags);

        cache.Update(new Item(1, "a", "y", "z"));

        Assert.Null(index.GetOne("x"));
        Assert.Equal(1, index.GetOne("y")!.Id);
        Assert.Equal(1, index.GetOne("z")!.Id);
    }

    [Fact]
    public void MultiValuedIndex_HandlesAnEntityWithNoValues()
    {
        var cache = Cache(new Item(1, "a"));
        var index = cache.CreateIndex(i => i.Tags);

        Assert.Empty(index.GetMultiple("x"));
        Assert.Equal([1], cache.GetAllKeys());
    }

    #endregion
}
