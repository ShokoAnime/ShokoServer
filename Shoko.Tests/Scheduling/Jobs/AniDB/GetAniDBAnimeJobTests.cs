using Shoko.Server.Scheduling.Jobs.AniDB;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.AniDB;

/// <summary>
/// What the merge of two waiting anime refreshes asks of AniDB.
/// </summary>
public class GetAniDBAnimeJobTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryMerge_ACacheFirstAndAForcedOnlineRefresh_GoRemoteFirstWithTheCacheAsFallback(bool cacheFirstWaits)
    {
        var cacheFirst = new GetAniDBAnimeJob(null!, null!, null!, null!) { UseCache = true, UseRemote = false, PreferCacheOverRemote = true };
        var online = new GetAniDBAnimeJob(null!, null!, null!, null!)
        {
            UseCache = false,
            UseRemote = true,
            PreferCacheOverRemote = false,
            IgnoreTimeCheck = true,
        };
        var (existing, incoming) = cacheFirstWaits ? (cacheFirst, online) : (online, cacheFirst);

        existing.TryMerge(incoming);

        Assert.True(existing.UseRemote);
        Assert.False(existing.PreferCacheOverRemote);
        Assert.True(existing.UseCache);
    }
}
