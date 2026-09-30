using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the per-entry locks the refresh and purge jobs share. A lock that
/// is free is taken before <c>Acquire</c> returns, so a wait shows at once.
/// </summary>
public class MetadataEntryLocksTests
{
    private static readonly MetadataGuid _series = new(TestSources.Plugin, MetadataEntityType.Series, "1");

    [Fact]
    public async Task ASecondHolderWaitsForTheFirst()
    {
        var locks = new MetadataEntryLocks();
        var first = await locks.Acquire(_series, TestContext.Current.CancellationToken);

        var second = locks.Acquire(_series, TestContext.Current.CancellationToken);
        Assert.False(second.IsCompleted);

        first.Dispose();
        (await second).Dispose();

        Assert.False(locks.IsInUse(_series));
    }

    [Fact]
    public async Task OtherEntriesAndSourcesDoNotWait()
    {
        var locks = new MetadataEntryLocks();
        using var held = await locks.Acquire(_series, TestContext.Current.CancellationToken);

        using var otherEntry = await locks.Acquire(new(TestSources.Plugin, MetadataEntityType.Series, "2"), TestContext.Current.CancellationToken);
        using var otherKind = await locks.Acquire(new(TestSources.Plugin, MetadataEntityType.Movie, "1"), TestContext.Current.CancellationToken);
        using var otherSource = await locks.Acquire(new(TestSources.AniList, MetadataEntityType.Series, "1"), TestContext.Current.CancellationToken);

        Assert.True(locks.IsInUse(_series));
    }

    [Fact]
    public async Task TheImageLockIsApartFromTheEntryLock()
    {
        var locks = new MetadataEntryLocks();
        using var entry = await locks.Acquire(_series, TestContext.Current.CancellationToken);

        var images = await locks.AcquireImages(_series, TestContext.Current.CancellationToken);
        var secondImages = locks.AcquireImages(_series, TestContext.Current.CancellationToken);
        Assert.False(secondImages.IsCompleted);

        images.Dispose();
        (await secondImages).Dispose();
    }

    [Fact]
    public async Task DisposingTwiceReleasesOnce()
    {
        var locks = new MetadataEntryLocks();
        var first = await locks.Acquire(_series, TestContext.Current.CancellationToken);
        first.Dispose();
        var second = await locks.Acquire(_series, TestContext.Current.CancellationToken);

        first.Dispose();
        var third = locks.Acquire(_series, TestContext.Current.CancellationToken);

        Assert.False(third.IsCompleted);
        second.Dispose();
        (await third).Dispose();
    }

    [Fact]
    public async Task ACancelledWaitLeavesTheLockAsItWas()
    {
        var locks = new MetadataEntryLocks();
        var held = await locks.Acquire(_series, TestContext.Current.CancellationToken);
        using var cancel = new CancellationTokenSource();

        var waiting = locks.Acquire(_series, cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        held.Dispose();
        Assert.False(locks.IsInUse(_series));
        (await locks.Acquire(_series, TestContext.Current.CancellationToken)).Dispose();
    }
}
