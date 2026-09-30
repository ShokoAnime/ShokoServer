using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Databases;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Tests.Providers.AniDB;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// The data fix filling <c>AniDB_Resource</c> from the cached anime XML, and queuing a refresh of
/// each anime whose XML is missing or unreadable.
/// </summary>
public class AnidbResourceFixupTests
{
    #region Filling

    [Fact]
    public void EachCachedAnimeIsFilledAndEachCacheMissIsQueued()
    {
        var cache = new Dictionary<int, string>
        {
            [16984] = AnidbResourceFixtures.Anime16984,
            [4459] = AnidbResourceFixtures.Anime4459,
            [7] = "<anime id=\"7\"><broken>",
        };
        var stored = new List<(int AnimeID, int Anime, int Episodes)>();
        var queued = new List<int>();
        var advanced = 0;

        var result = DatabaseFixes.FillAnidbResources(
            [16984, 4459, 8, 7],
            animeID => cache.GetValueOrDefault(animeID),
            new HttpAnimeParser(NullLogger<HttpAnimeParser>.Instance),
            (response, animeID) => stored.Add((animeID, response.Resources.Count, response.Episodes.Sum(episode => episode.Resources.Count))),
            queued.Add,
            () => advanced++
        );

        Assert.Equal([(16984, 19, 4), (4459, 7, 0)], stored);
        Assert.Equal([8, 7], queued);
        Assert.Equal(new DatabaseFixes.AnidbResourceFillResult(2, 26, 4, 2), result);
        Assert.Equal(4, advanced);
    }

    [Fact]
    public void AFailedStoreIsLoggedAndTheRestGoOn()
    {
        var queued = new List<int>();

        var result = DatabaseFixes.FillAnidbResources(
            [16984, 4459],
            animeID => animeID is 16984 ? AnidbResourceFixtures.Anime16984 : AnidbResourceFixtures.Anime4459,
            new HttpAnimeParser(NullLogger<HttpAnimeParser>.Instance),
            (_, animeID) => { if (animeID is 16984) throw new InvalidOperationException("The write was refused."); },
            queued.Add
        );

        Assert.Equal(new DatabaseFixes.AnidbResourceFillResult(1, 7, 0, 0), result);
        Assert.Empty(queued);
    }

    #endregion

    #region Queuing

    [Fact]
    public void ACacheMissQueuesARefreshThatSkipsOtherSources()
    {
        Action<GetAniDBAnimeJob>? configure = null;
        var scheduler = new Mock<IQueueScheduler>();
        scheduler
            .Setup(queue => queue.Enqueue(It.IsAny<Action<GetAniDBAnimeJob>?>(), false, null, It.IsAny<CancellationToken>()))
            .Callback<Action<GetAniDBAnimeJob>?, bool, DateTimeOffset?, CancellationToken>((action, _, _, _) => configure = action)
            .Returns(Task.CompletedTask);

        DatabaseFixes.QueueAnidbResourceRefresh(scheduler.Object, 8);

        var job = new GetAniDBAnimeJob(null!, null!, null!, null!);
        Assert.NotNull(configure);
        configure(job);
        Assert.Equal((8, true), (job.AnimeID, job.SkipSupplementaryUpdate));
    }

    #endregion
}
