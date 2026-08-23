using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// <see cref="MyListFetchMode"/> mixes transport flags with modifier flags, so
/// a caller asking only for a modifier still has to end up with a transport.
/// These cover how the service resolves that.
/// </summary>
public class MyListFetchModeTests
{
    private static MyListService CreateService(MyListFetchMode configured)
    {
        var settings = new ServerSettings();
        settings.AniDb.MyList_FetchMode = configured;
        var settingsProvider = new Mock<ISettingsProvider>();
        settingsProvider.Setup(a => a.GetSettings(It.IsAny<bool>())).Returns(settings);
        // named so that reordering the constructor cannot silently swap the two
        // arguments this fixture actually depends on
        return new MyListService(
            logger: NullLogger<MyListService>.Instance,
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
            anidbEpisodes: null!,
            storedReleaseInfos: null!,
            seriesService: null!
        );
    }

    [Fact]
    public void ResolveFetchMode_AutoBecomesTheConfiguredMode()
    {
        var service = CreateService(MyListFetchMode.Http | MyListFetchMode.Cache);

        Assert.Equal(MyListFetchMode.Http | MyListFetchMode.Cache, service.ResolveFetchMode(MyListFetchMode.Auto));
    }

    [Fact]
    public void ResolveFetchMode_ModifierOnlyKeepsTheConfiguredTransports()
    {
        var service = CreateService(MyListFetchMode.Default);

        var resolved = service.ResolveFetchMode(MyListFetchMode.IgnoreTimeCheck);

        Assert.True(resolved.HasFlag(MyListFetchMode.IgnoreTimeCheck));
        Assert.True(resolved.HasFlag(MyListFetchMode.Http));
        Assert.True(resolved.HasFlag(MyListFetchMode.Udp));
        Assert.True(resolved.HasFlag(MyListFetchMode.Cache));
    }

    [Fact]
    public void ResolveFetchMode_ModifierOnlyDoesNotWidenBeyondTheConfiguredTransports()
    {
        var service = CreateService(MyListFetchMode.Cache);

        var resolved = service.ResolveFetchMode(MyListFetchMode.IgnoreTimeCheck);

        Assert.True(resolved.HasFlag(MyListFetchMode.Cache));
        Assert.False(resolved.HasFlag(MyListFetchMode.Http));
        Assert.False(resolved.HasFlag(MyListFetchMode.Udp));
    }

    [Fact]
    public void ResolveFetchMode_AnExplicitTransportIsLeftAlone()
    {
        var service = CreateService(MyListFetchMode.Default);

        Assert.Equal(MyListFetchMode.Cache, service.ResolveFetchMode(MyListFetchMode.Cache));
    }

    [Fact]
    public async Task SyncAsync_SkipsWhenASyncIsAlreadyRunning()
    {
        var service = CreateService(MyListFetchMode.Default);

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
        var service = CreateService(MyListFetchMode.Default);

        // the fixture's null dependencies make the body throw; the guard must
        // still come back, or every later sync would be skipped forever
        await Assert.ThrowsAnyAsync<Exception>(() => service.SyncAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.True(await service._syncLock.WaitAsync(0, TestContext.Current.CancellationToken));
        service._syncLock.Release();
    }

    [Fact]
    public void ResolveFetchMode_NoneStaysNone()
    {
        var service = CreateService(MyListFetchMode.Default);

        Assert.Equal(MyListFetchMode.None, service.ResolveFetchMode(MyListFetchMode.None));
    }
}
