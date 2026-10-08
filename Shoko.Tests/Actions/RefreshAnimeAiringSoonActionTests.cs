using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Actions;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Actions;

/// <summary>
/// The anime airing soon are refreshed from AniDB when they are in the
/// collection, air within the window, and were not updated recently.
/// </summary>
public class RefreshAnimeAiringSoonActionTests
{
    #region Helpers

    /// <summary>
    /// An anime in the test: whether it has a series, when its next episode
    /// airs from now, or on which day from today in UTC for a date-only entry,
    /// how long ago it was updated, if ever, and the anime in the collection
    /// its airing's series is linked to.
    /// </summary>
    private sealed record Anime(
        int ID,
        bool HasSeries,
        TimeSpan? AirsIn = null,
        int? AirsOnDay = null,
        TimeSpan? UpdatedAgo = null,
        int[]? Linked = null
    );

    /// <summary>
    /// Runs the action over the anime with the default airing settings and
    /// returns the jobs it queued.
    /// </summary>
    private static Task<List<GetAniDBAnimeJob>> Run(int minimumHours, params Anime[] anime)
        => Run(minimumHours, new AiringScheduleServiceSettings(), anime);

    /// <summary>
    /// Runs the action over the anime and returns the jobs it queued.
    /// </summary>
    private static async Task<List<GetAniDBAnimeJob>> Run(int minimumHours, AiringScheduleServiceSettings airingSettings, params Anime[] anime)
    {
        var queued = new List<GetAniDBAnimeJob>();
        var action = Build(DateTime.UtcNow, minimumHours, airingSettings, queued, anime);
        await action.Execute(new Progress<decimal>(), TestContext.Current.CancellationToken);
        return queued;
    }

    /// <summary>
    /// Builds the action over the anime, airing relative to
    /// <paramref name="now"/>, queuing its jobs into <paramref name="queued"/>.
    /// </summary>
    private static RefreshAnimeAiringSoonAction Build(
        DateTime now,
        int minimumHours,
        AiringScheduleServiceSettings airingSettings,
        List<GetAniDBAnimeJob> queued,
        Anime[] anime
    )
    {
        var airings = anime.Select(entry =>
        {
            var episode = new Mock<IAnidbEpisode>();
            episode.SetupGet(e => e.AnidbAnimeID).Returns(entry.ID);
            var airing = new Mock<IEpisodeAiring>();
            if (entry.AirsOnDay is { } day)
            {
                airing.SetupGet(a => a.IsDateOnly).Returns(true);
                airing.SetupGet(a => a.AirDate).Returns(DateOnly.FromDateTime(now.Date.AddDays(day)));
            }
            else
            {
                airing.SetupGet(a => a.AiredAt).Returns(now + entry.AirsIn);
            }

            airing.SetupGet(a => a.AnidbEpisode).Returns(episode.Object);
            if (entry.Linked is { } linked)
            {
                var links = linked.Select(id => Mock.Of<IMetadataSeriesCrossReference>(link => link.AnidbAnimeID == id)).ToList();
                var series = Mock.Of<ISeries>(s => s.MetadataSeriesCrossReferences == links);
                airing.SetupGet(a => a.Episode).Returns(Mock.Of<IEpisode>(e => e.Series == series));
            }

            return airing.Object;
        }).ToList();

        // Answers the range as the service does: instants for timed airings, UTC dates for date-only ones.
        var airingService = new Mock<IAiringScheduleService>();
        airingService.Setup(s => s.GetAiringsInRange(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<EpisodeAiringFilteringOptions?>()))
            .Returns((DateTimeOffset from, DateTimeOffset to, EpisodeAiringFilteringOptions? options) => airings
                .Where(a => a.IsDateOnly
                    ? options?.IncludeDateOnly is true
                        && a.AirDate >= DateOnly.FromDateTime(from.UtcDateTime)
                        && a.AirDate <= DateOnly.FromDateTime(to.UtcDateTime.AddTicks(-1))
                    : a.AiredAt >= from.UtcDateTime && a.AiredAt < to.UtcDateTime
                )
                .ToList()
            );

        var seriesAnimeIDs = anime.Where(entry => entry.HasSeries).Select(entry => entry.ID)
            .Concat(anime.SelectMany(entry => entry.Linked ?? []))
            .Distinct();
        var series = CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(
            row => row.AnimeSeriesID,
            seriesAnimeIDs.Select(id => new AnimeSeries { AnimeSeriesID = id, AniDB_ID = id })
        );
        var updates = new Mock<AniDB_AnimeUpdateRepository>((DatabaseFactory)null!);
        updates.Setup(r => r.GetByAnimeID(It.IsAny<int>()))
            .Returns((int id) => anime.FirstOrDefault(entry => entry.ID == id)?.UpdatedAgo is { } ago
                ? new AniDB_AnimeUpdate { AnimeID = id, UpdatedAt = DateTime.Now - ago }
                : null
            );

        var settings = new ServerSettings();
        settings.AniDb.MinimumHoursToRedownloadAnimeInfo = minimumHours;
        var configurationService = new Mock<IConfigurationService>();
        configurationService.Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>())).Returns(airingSettings);

        var scheduler = new Mock<IQueueScheduler>();
        scheduler.Setup(s => s.Enqueue(It.IsAny<Action<GetAniDBAnimeJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Callback((Action<GetAniDBAnimeJob>? configure, bool _, DateTimeOffset? _, CancellationToken _) =>
            {
                var job = new GetAniDBAnimeJob(null!, null!, null!, null!);
                configure?.Invoke(job);
                queued.Add(job);
            })
            .Returns(Task.CompletedTask);

        return new RefreshAnimeAiringSoonAction(
            NullLogger<RefreshAnimeAiringSoonAction>.Instance,
            new StubSettingsProvider(settings),
            new ConfigurationProvider<AiringScheduleServiceSettings>(configurationService.Object),
            SuspensionTestDoubles.Service().Object,
            airingService.Object,
            series,
            updates.Object,
            scheduler.Object
        );
    }

    #endregion

    #region Tests

    [Fact]
    public async Task AStaleAnimeAiringWithinADay_IsQueuedForAForcedOnlineRefresh()
    {
        var queued = await Run(
            24,
            new Anime(1, HasSeries: true, AirsIn: TimeSpan.FromHours(3), UpdatedAgo: TimeSpan.FromHours(30)),
            new Anime(2, HasSeries: true, AirsIn: TimeSpan.FromHours(23))
        );

        Assert.Equal([1, 2], queued.Select(job => job.AnimeID));
        Assert.All(queued, job =>
        {
            Assert.True(job.UseRemote);
            Assert.False(job.PreferCacheOverRemote);
            Assert.True(job.IgnoreTimeCheck);
            Assert.False(job.IgnoreHttpBans);
            Assert.False(job.SkipSupplementaryUpdate);
        });
    }

    [Fact]
    public async Task AnAnimeUpdatedRecently_IsSkipped()
    {
        var queued = await Run(24, new Anime(1, HasSeries: true, AirsIn: TimeSpan.FromHours(3), UpdatedAgo: TimeSpan.FromHours(20)));

        Assert.Empty(queued);
    }

    [Theory]
    [InlineData(24, false)]
    [InlineData(48, true)]
    public async Task TheWindowSetting_DecidesWhetherAnAiringIn36HoursCounts(int windowHours, bool expected)
    {
        var queued = await Run(
            24,
            new AiringScheduleServiceSettings { AiringSoonWindowHours = windowHours },
            new Anime(1, HasSeries: true, AirsIn: TimeSpan.FromHours(36))
        );

        Assert.Equal(expected, queued.Count is 1);
    }

    [Fact]
    public async Task TheAnimeLinkedToTheAiringsSeries_AreQueuedAlongsideItsOwn()
    {
        var queued = await Run(24, new Anime(1, HasSeries: true, AirsIn: TimeSpan.FromHours(3), Linked: [1, 2, 3]));

        Assert.Equal([1, 2, 3], queued.Select(job => job.AnimeID));
    }

    [Theory]
    [InlineData(10, 24, false, 0, false)]
    [InlineData(10, 6, true, 0, true)]
    [InlineData(10, 6, true, 1, false)]
    [InlineData(23, 2, true, 0, true)]
    [InlineData(23, 2, true, 1, true)]
    [InlineData(10, 72, true, 3, true)]
    [InlineData(10, 72, true, 4, false)]
    public void ADateOnlyEntry_CountsWhenEnabledAndItsUtcDayOverlapsTheWindow(int hour, int windowHours, bool includeDateOnly, int day, bool expected)
    {
        var now = new DateTime(2026, 10, 6, hour, 0, 0, DateTimeKind.Utc);
        var action = Build(now, 24, new AiringScheduleServiceSettings(), [], [new Anime(1, HasSeries: true, AirsOnDay: day)]);

        var animeIDs = action.GetAnimeAiringSoon(now, TimeSpan.FromHours(windowHours), includeDateOnly);

        Assert.Equal(expected, animeIDs.Count is 1);
    }

    [Fact]
    public async Task AnAnimeWithoutASeries_IsSkipped()
    {
        var queued = await Run(24, new Anime(1, HasSeries: false, AirsIn: TimeSpan.FromHours(3)));

        Assert.Empty(queued);
    }

    [Fact]
    public async Task AMinimumOfZeroHours_FloorsToOneHour()
    {
        var queued = await Run(
            0,
            new Anime(1, HasSeries: true, AirsIn: TimeSpan.FromHours(3), UpdatedAgo: TimeSpan.FromMinutes(30)),
            new Anime(2, HasSeries: true, AirsIn: TimeSpan.FromHours(3), UpdatedAgo: TimeSpan.FromMinutes(90))
        );

        Assert.Equal([2], queued.Select(job => job.AnimeID));
    }

    #endregion
}
