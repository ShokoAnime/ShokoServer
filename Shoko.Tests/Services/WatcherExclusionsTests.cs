using System;
using System.Threading.Tasks;
using Shoko.Server.Services.FileSystemWatcher;
using Xunit;

namespace Shoko.Tests.Services;

public class WatcherExclusionsTests
{
    private static readonly TimeSpan _grace = TimeSpan.FromMilliseconds(100);

    private static Task AfterGrace()
        => Task.Delay(_grace * 4, TestContext.Current.CancellationToken);

    /// <summary>
    ///   Waits for a hold to be lifted, as long as a busy test run may need.
    /// </summary>
    private static async Task<bool> Lifted(WatcherExclusions exclusions, string path)
    {
        for (var waited = TimeSpan.Zero; waited < TimeSpan.FromSeconds(5); waited += _grace)
        {
            if (!exclusions.Contains(path))
                return true;
            await Task.Delay(_grace, TestContext.Current.CancellationToken);
        }

        return false;
    }

    [Fact]
    public async Task Release_LiftsTheHoldOnlyAfterTheGracePeriod()
    {
        var exclusions = new WatcherExclusions(_grace);

        exclusions.Hold("/media/a.mkv");
        exclusions.Release("/media/a.mkv");

        Assert.True(exclusions.Contains("/media/a.mkv"));
        Assert.True(await Lifted(exclusions, "/media/a.mkv"));
        Assert.False(exclusions.Any);
    }

    [Fact]
    public async Task OverlappingHolds_StandUntilBothAreReleased()
    {
        var exclusions = new WatcherExclusions(_grace);

        exclusions.Hold("/media/a.mkv");
        exclusions.Hold("/media/a.mkv");
        exclusions.Release("/media/a.mkv");
        await AfterGrace();

        Assert.True(exclusions.Contains("/media/a.mkv"));
        exclusions.Release("/media/a.mkv");
        Assert.True(await Lifted(exclusions, "/media/a.mkv"));
    }

    [Fact]
    public void Contains_MatchesTheExactPath()
    {
        var exclusions = new WatcherExclusions(_grace);

        exclusions.Hold("/media/a.mkv");

        Assert.False(exclusions.Contains("/media/A.mkv"));
        Assert.False(exclusions.Contains("/media"));
    }
}
