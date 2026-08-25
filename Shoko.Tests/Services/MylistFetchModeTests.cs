using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Server.Services.Mylist;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// <see cref="MylistFetchMode"/> mixes transport flags with modifier flags, so
/// a caller asking only for a modifier still has to end up with a transport.
/// These cover how the service resolves that.
/// </summary>
public class MylistFetchModeTests
{
    private static MylistService CreateService(MylistFetchMode configured)
    {
        var settings = new ServerSettings();
        settings.AniDb.MyList_FetchMode = configured;
        var settingsProvider = new Mock<ISettingsProvider>();
        settingsProvider.Setup(a => a.GetSettings(It.IsAny<bool>())).Returns(settings);
        // named so that reordering the constructor cannot silently swap the two
        // arguments this fixture actually depends on
        return new MylistService(
            logger: NullLogger<MylistService>.Instance,
            requestFactory: null!,
            scheduler: null!,
            settingsProvider: settingsProvider.Object,
            applicationPaths: null!,
            userDataService: null!,
            mylistCache: null!,
            genericsCache: null!,
            users: null!,
            videoLocals: null!,
            videoLocalUsers: null!,
            animeEpisodes: null!,
            animeEpisodeUsers: null!,
            anidbEpisodes: null!,
            storedReleaseInfos: null!,
            seriesService: null!
        );
    }

    [Fact]
    public void ResolveFetchMode_AutoBecomesTheConfiguredMode()
    {
        var service = CreateService(MylistFetchMode.Http | MylistFetchMode.Cache);

        Assert.Equal(MylistFetchMode.Http | MylistFetchMode.Cache, service.ResolveFetchMode(MylistFetchMode.Auto));
    }

    [Fact]
    public void ResolveFetchMode_ModifierOnlyKeepsTheConfiguredTransports()
    {
        var service = CreateService(MylistFetchMode.Default);

        var resolved = service.ResolveFetchMode(MylistFetchMode.IgnoreTimeCheck);

        Assert.True(resolved.HasFlag(MylistFetchMode.IgnoreTimeCheck));
        Assert.True(resolved.HasFlag(MylistFetchMode.Http));
        Assert.True(resolved.HasFlag(MylistFetchMode.Udp));
        Assert.True(resolved.HasFlag(MylistFetchMode.Cache));
    }

    [Fact]
    public void ResolveFetchMode_ModifierOnlyDoesNotWidenBeyondTheConfiguredTransports()
    {
        var service = CreateService(MylistFetchMode.Cache);

        var resolved = service.ResolveFetchMode(MylistFetchMode.IgnoreTimeCheck);

        Assert.True(resolved.HasFlag(MylistFetchMode.Cache));
        Assert.False(resolved.HasFlag(MylistFetchMode.Http));
        Assert.False(resolved.HasFlag(MylistFetchMode.Udp));
    }

    [Fact]
    public void ResolveFetchMode_AnExplicitTransportIsLeftAlone()
    {
        var service = CreateService(MylistFetchMode.Default);

        Assert.Equal(MylistFetchMode.Cache, service.ResolveFetchMode(MylistFetchMode.Cache));
    }

    [Fact]
    public async Task SyncAsync_SkipsWhenASyncIsAlreadyRunning()
    {
        var service = CreateService(MylistFetchMode.Default);

        // stand in for a sync already in flight; without the guard the body runs
        // and trips over this fixture's null dependencies
        Assert.True(await service._syncLock.WaitAsync(0, TestContext.Current.CancellationToken));
        try
        {
            Assert.Null(await service.SyncAsync(cancellationToken: TestContext.Current.CancellationToken));
        }
        finally
        {
            service._syncLock.Release();
        }
    }

    [Fact]
    public async Task SyncAsync_ReleasesTheGuardWhenTheSyncThrows()
    {
        var service = CreateService(MylistFetchMode.Default);

        // the fixture's null dependencies make the body throw; the guard must
        // still come back, or every later sync would be skipped forever
        await Assert.ThrowsAnyAsync<Exception>(() => service.SyncAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.True(await service._syncLock.WaitAsync(0, TestContext.Current.CancellationToken));
        service._syncLock.Release();
    }

    [Theory]
    [InlineData("global")]
    [InlineData("videos")]
    [InlineData("episodes")]
    public void ScheduleSync_RefusesAPreview(string scope)
    {
        var service = CreateService(MylistFetchMode.Default);
        var options = new MylistSyncOptions { PlanOnly = true };

        // a queued job has nowhere to return a plan, so asking for one is a
        // mistake rather than a no-op — and quietly running it would write
        // exactly what the caller asked not to
        // the guard throws synchronously, before any task is produced
        Assert.Throws<ArgumentException>(() =>
        {
            _ = scope switch
            {
                "videos" => service.ScheduleSync(Array.Empty<IVideo>(), options),
                "episodes" => service.ScheduleSync(Array.Empty<IShokoEpisode>(), options),
                _ => service.ScheduleSync(options),
            };
        });
    }

    [Fact]
    public void ScheduleSync_AllowsANonPreview()
    {
        var service = CreateService(MylistFetchMode.Default);

        // an empty set short-circuits before touching this fixture's null deps
        Assert.NotNull(service.ScheduleSync(Array.Empty<IShokoEpisode>(), new MylistSyncOptions { PlanOnly = false }));
    }

    [Fact]
    public void ResolveFetchMode_NoneStaysNone()
    {
        var service = CreateService(MylistFetchMode.Default);

        Assert.Equal(MylistFetchMode.None, service.ResolveFetchMode(MylistFetchMode.None));
    }
}
