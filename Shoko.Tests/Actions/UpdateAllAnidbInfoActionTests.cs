using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Server.Actions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Actions;

/// <summary>
/// A scheduled action queuing one job per item counts its items into the
/// progress, ends at 100, and stops queuing once cancelled.
/// </summary>
public class UpdateAllAnidbInfoActionTests
{
    #region Helpers

    /// <summary>
    /// Keeps every value reported, in order.
    /// </summary>
    private sealed class ListProgress : IProgress<decimal>
    {
        public List<decimal> Values { get; } = [];

        public void Report(decimal value) => Values.Add(value);
    }

    private static AniDB_AnimeRepository Anime(int count)
        => CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(
            anime => anime.AniDB_AnimeID,
            Enumerable.Range(1, count).Select(id => new AniDB_Anime { AniDB_AnimeID = id, AnimeID = id })
        );

    #endregion

    #region Tests

    [Fact]
    public async Task EveryAnimeIsQueued_AndTheProgressEndsAt100()
    {
        var queued = new List<int>();
        var anidb = new Mock<IAnidbService>();
        anidb.Setup(service => service.ScheduleRefreshOfAnime(It.IsAny<IAnidbAnime>(), It.IsAny<AnidbRefreshMethod>(), It.IsAny<bool>()))
            .Callback((IAnidbAnime anime, AnidbRefreshMethod _, bool _) => queued.Add(anime.AnidbID))
            .Returns(Task.CompletedTask);
        var progress = new ListProgress();

        await new UpdateAllAnidbInfoAction(anidb.Object, Anime(4)).Execute(progress, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3, 4], queued.Order());
        Assert.Equal([0m, 25m, 50m, 75m, 100m], progress.Values);
    }

    [Fact]
    public async Task ACancelledRun_StopsQueuing()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var queued = 0;
        var anidb = new Mock<IAnidbService>();
        anidb.Setup(service => service.ScheduleRefreshOfAnime(It.IsAny<IAnidbAnime>(), It.IsAny<AnidbRefreshMethod>(), It.IsAny<bool>()))
            .Callback(() =>
            {
                if (++queued is 2)
                    cancellation.Cancel();
            })
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new UpdateAllAnidbInfoAction(anidb.Object, Anime(4)).Execute(new ListProgress(), cancellation.Token)
        );

        Assert.Equal(2, queued);
    }

    #endregion
}
