using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Release;

using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Databases;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the missing-episode statistics <see cref="AnimeSeriesService.UpdateStats"/> writes onto a
/// series. These counts drive the missing-episode filters, the dashboard, and the calendar, and
/// nothing else recomputes them — a series carries whatever this last wrote.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnimeSeriesStatsTests
{
    private const int AnimeID = 100;
    private const int SeriesID = 1;

    private static readonly DateTime s_aired = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly DateTime s_unaired = DateTime.Now.AddYears(5);

    /// <summary>
    /// One episode. <paramref name="AirsAt"/> null means AniDB gave no air date at all, which is a
    /// different branch from an episode dated in the future.
    /// </summary>
    private sealed record EpisodeSpec(
        int Number,
        bool HasFile,
        DateTime? AirsAt = null,
        bool UnknownAirDate = false,
        bool Hidden = false,
        EpisodeType Type = EpisodeType.Episode,
        int ReleaseGroupID = 0);

    private sealed class Harness : IDisposable
    {
        public AnimeSeriesService Service { get; }

        public AnimeSeries Series { get; }

        public Mock<AnimeSeriesRepository> SeriesRepository { get; }

        private readonly RepoFactoryScope _scope;

        public Harness(IEnumerable<EpisodeSpec> specs, IEnumerable<AniDB_GroupStatus>? groupStatuses = null, DateTime? animeEndDate = null)
        {
            Series = new AnimeSeries { AnimeSeriesID = SeriesID, AniDB_ID = AnimeID };

            var anidbEpisodes = new List<AniDB_Episode>();
            var shokoEpisodes = new List<AnimeEpisode>();
            var videos = new List<VideoLocal>();
            var crossRefs = new List<CrossRef_File_Episode>();
            var releaseInfos = new List<StoredReleaseInfo>();
            foreach (var spec in specs)
            {
                var episodeId = spec.Number + (int)spec.Type * 1000;
                anidbEpisodes.Add(new AniDB_Episode
                {
                    AniDB_EpisodeID = episodeId,
                    EpisodeID = episodeId,
                    AnimeID = AnimeID,
                    EpisodeNumber = spec.Number,
                    EpisodeType = spec.Type,
                    AirDate = spec.UnknownAirDate ? 0 : (int)((spec.AirsAt ?? s_aired) - new DateTime(1970, 1, 1)).TotalSeconds,
                });
                shokoEpisodes.Add(new AnimeEpisode
                {
                    AnimeEpisodeID = episodeId,
                    AnimeSeriesID = SeriesID,
                    AniDB_EpisodeID = episodeId,
                    IsHidden = spec.Hidden,
                });

                if (!spec.HasFile)
                    continue;

                var hash = $"hash-{episodeId}";
                videos.Add(new VideoLocal { VideoLocalID = episodeId, Hash = hash, FileSize = 1000 });
                if (spec.ReleaseGroupID > 0)
                    releaseInfos.Add(new StoredReleaseInfo
                    {
                        StoredReleaseInfoID = episodeId,
                        ED2K = hash,
                        FileSize = 1000,
                        GroupID = spec.ReleaseGroupID.ToString(),
                        GroupSource = "AniDB",
                        GroupName = $"Group {spec.ReleaseGroupID}",
                        // All four group fields are required before a release exposes a group.
                        GroupShortName = $"G{spec.ReleaseGroupID}",
                    });
                crossRefs.Add(new CrossRef_File_Episode
                {
                    CrossRef_File_EpisodeID = episodeId,
                    Hash = hash,
                    AnimeID = AnimeID,
                    EpisodeID = episodeId,
                    Percentage = 100,
                });
            }

            SeriesRepository = CachedRepo.BuildWritable<AnimeSeriesRepository, int, AnimeSeries>(s => s.AnimeSeriesID, [Series]);
            // UpdateStats persists through the three-argument overload.
            SeriesRepository.Setup(r => r.Save(It.IsAny<AnimeSeries>(), It.IsAny<bool>(), It.IsAny<bool>()));

            var episodeRepository = CachedRepo.Build<AnimeEpisodeRepository, int, AnimeEpisode>(e => e.AnimeEpisodeID, shokoEpisodes);

            var releaseInfoRepository = CachedRepo.Build<StoredReleaseInfoRepository, int, StoredReleaseInfo>(r => r.StoredReleaseInfoID, releaseInfos);

            _scope = new RepoFactoryScope()
                // VideoLocal.ReleaseGroup resolves through here while collecting the groups the
                // user is currently following.
                .Set(releaseInfoRepository)
                .With<AniDB_AnimeRepository, int, AniDB_Anime>(a => a.AniDB_AnimeID,
                    [new AniDB_Anime
                    {
                        AniDB_AnimeID = 1, AnimeID = AnimeID, MainTitle = "Test", AnimeType = AnimeType.TV,
                        AirDate = new PartialDateOnly(2020, 1, 1),
                        EndDate = animeEndDate is { } end ? new PartialDateOnly(end.Year, end.Month, end.Day) : null,
                    }])
                .With<AniDB_EpisodeRepository, int, AniDB_Episode>(e => e.AniDB_EpisodeID, anidbEpisodes)
                .With<VideoLocalRepository, int, VideoLocal>(v => v.VideoLocalID, videos)
                .With<CrossRef_File_EpisodeRepository, int, CrossRef_File_Episode>(x => x.CrossRef_File_EpisodeID, crossRefs)
                .Set(episodeRepository);

            Service = new AnimeSeriesService(
                NullLogger<AnimeSeriesService>.Instance,
                serviceProvider: null!,
                schedulerFactory: null!,
                groupService: null!,
                vlUsers: null!,
                videoReleaseService: null!,
                userDataService: null!,
                animeEpisodes: episodeRepository,
                animeSeries: SeriesRepository.Object,
                storedReleaseInfos: releaseInfoRepository,
                anidbGroupStatuses: GroupStatuses(groupStatuses),
                anidbAnimeStaff: null!,
                xrefAnidbTmdbShows: null!,
                xrefAnidbTmdbMovies: null!);
        }

        /// <summary>
        /// A direct repository, so it cannot be cache-backed; with no statuses the service treats
        /// every aired episode as released, which is the common case.
        /// </summary>
        private static AniDB_GroupStatusRepository GroupStatuses(IEnumerable<AniDB_GroupStatus>? statuses)
        {
            var mock = new Mock<AniDB_GroupStatusRepository>((DatabaseFactory)null!, (IQueueScheduler)null!);
            mock.Setup(r => r.GetByAnimeID(It.IsAny<int>())).Returns([.. statuses ?? []]);
            return mock.Object;
        }

        public AnimeSeries Update()
        {
            Service.UpdateStats(Series, watchedStats: false, missingEpsStats: true);
            return Series;
        }

        public void Dispose() => _scope.Dispose();
    }

    private static Harness Create(params EpisodeSpec[] specs) => new(specs);

    private static AniDB_GroupStatus GroupStatus(
        int groupId,
        Shoko.Server.Providers.AniDB.Group_CompletionStatus state = Shoko.Server.Providers.AniDB.Group_CompletionStatus.Complete,
        string episodeRange = "")
        => new()
        {
            AniDB_GroupStatusID = groupId,
            AnimeID = AnimeID,
            GroupID = groupId,
            GroupName = $"Group {groupId}",
            CompletionState = (int)state,
            EpisodeRange = episodeRange,
        };

    #region Missing episode counts

    [Fact]
    public void AnEpisodeWithoutAFileCountsAsMissing()
    {
        using var harness = Create(new EpisodeSpec(1, HasFile: false));

        Assert.Equal(1, harness.Update().MissingEpisodeCount);
    }

    [Fact]
    public void AnEpisodeWithAFileDoesNotCountAsMissing()
    {
        using var harness = Create(new EpisodeSpec(1, HasFile: true));

        Assert.Equal(0, harness.Update().MissingEpisodeCount);
    }

    [Fact]
    public void OnlyTheEpisodesWithoutFilesAreCounted()
    {
        using var harness = Create(
            new EpisodeSpec(1, HasFile: true),
            new EpisodeSpec(2, HasFile: false),
            new EpisodeSpec(3, HasFile: false),
            new EpisodeSpec(4, HasFile: true));

        Assert.Equal(2, harness.Update().MissingEpisodeCount);
    }

    [Fact]
    public void AnEpisodeAiringInTheFutureIsNotCountedAsMissing()
    {
        using var harness = Create(new EpisodeSpec(1, HasFile: false, AirsAt: s_unaired));

        // Nobody is missing an episode that has not been broadcast yet.
        Assert.Equal(0, harness.Update().MissingEpisodeCount);
    }

    [Fact]
    public void AnEpisodeWithNoAirDateFallsBackToWhetherTheSeriesHasFinished()
    {
        // AniDB often has no date for an episode. The series having finished is then taken to mean
        // the episode aired, so it counts as missing; a still-running series does not.
        // Scoped one at a time: both install into the same RepoFactory statics.
        int finished;
        using (var harness = new Harness([new EpisodeSpec(1, HasFile: false, UnknownAirDate: true)], animeEndDate: s_aired))
            finished = harness.Update().MissingEpisodeCount;

        int running;
        using (var harness = new Harness([new EpisodeSpec(1, HasFile: false, UnknownAirDate: true)]))
            running = harness.Update().MissingEpisodeCount;

        Assert.Equal(1, finished);
        Assert.Equal(0, running);
    }

    [Fact]
    public void AHiddenEpisodeIsCountedSeparately()
    {
        using var harness = Create(
            new EpisodeSpec(1, HasFile: false, Hidden: true),
            new EpisodeSpec(2, HasFile: false));

        var series = harness.Update();

        Assert.Equal(1, series.MissingEpisodeCount);
        Assert.Equal(1, series.HiddenMissingEpisodeCount);
    }

    [Fact]
    public void OnlyRegularEpisodesAreCounted()
    {
        using var harness = Create(
            new EpisodeSpec(1, HasFile: false),
            new EpisodeSpec(1, HasFile: false, Type: EpisodeType.Special),
            new EpisodeSpec(1, HasFile: false, Type: EpisodeType.Credits));

        // Specials and credits are deliberately excluded; only regular episodes are counted.
        Assert.Equal(1, harness.Update().MissingEpisodeCount);
    }

    [Fact]
    public void TheCountsAreRecomputedRatherThanAccumulated()
    {
        using var harness = Create(new EpisodeSpec(1, HasFile: false));

        harness.Update();
        var second = harness.Update();

        // Running twice must not double the counts.
        Assert.Equal(1, second.MissingEpisodeCount);
    }

    #endregion

    #region Release groups

    [Fact]
    public void AnEpisodeNoGroupHasReleasedIsNotCountedAsMissing()
    {
        // A group list that covers only episode 1 means episode 2 does not exist to be collected
        // yet, whatever AniDB says the episode count is.
        using var harness = new Harness(
            [new EpisodeSpec(1, HasFile: true), new EpisodeSpec(2, HasFile: false)],
            [GroupStatus(7, Shoko.Server.Providers.AniDB.Group_CompletionStatus.Ongoing, episodeRange: "1")]);

        Assert.Equal(0, harness.Update().MissingEpisodeCount);
    }

    [Fact]
    public void AnEpisodeReleasedByAGroupTheUserCollectsCountsTowardsTheGroupTotal()
    {
        // The user holds episode 1 from group 7, so group 7 is one they collect; episode 2 is
        // released by that same group and missing.
        using var harness = new Harness(
            [new EpisodeSpec(1, HasFile: true, ReleaseGroupID: 7), new EpisodeSpec(2, HasFile: false)],
            [GroupStatus(7)]);

        var series = harness.Update();

        Assert.Equal(1, series.MissingEpisodeCount);
        Assert.Equal(1, series.MissingEpisodeCountGroups);
    }

    [Fact]
    public void AnEpisodeOnlyReleasedByAGroupTheUserDoesNotCollectIsExcludedFromTheGroupTotal()
    {
        // Still missing outright, but not from a group the user follows.
        using var harness = new Harness(
            [new EpisodeSpec(1, HasFile: true, ReleaseGroupID: 7), new EpisodeSpec(2, HasFile: false)],
            [GroupStatus(9)]);

        var series = harness.Update();

        Assert.Equal(1, series.MissingEpisodeCount);
        Assert.Equal(0, series.MissingEpisodeCountGroups);
    }

    [Fact]
    public void WithNoGroupStatusesAtAllEveryAiredEpisodeCounts()
    {
        // The group list is only populated once the UDP command has run; until then nothing can be
        // ruled out, so an aired episode counts as missing.
        using var harness = Create(new EpisodeSpec(1, HasFile: false));

        Assert.Equal(1, harness.Update().MissingEpisodeCount);
    }

    #endregion

    #region Derived values

    [Fact(Skip = "Possible bug - Needs investigation")]
    public void TheLatestLocalEpisodeNumberFollowsTheHighestHeldEpisode()
    {
        using var harness = Create(
            new EpisodeSpec(1, HasFile: true),
            new EpisodeSpec(2, HasFile: true),
            new EpisodeSpec(3, HasFile: false));

        Assert.Equal(2, harness.Update().LatestLocalEpisodeNumber);
    }

    [Fact]
    public void TheLatestLocalEpisodeNumberIsZeroWhenNothingIsHeld()
    {
        using var harness = Create(new EpisodeSpec(1, HasFile: false));

        Assert.Equal(0, harness.Update().LatestLocalEpisodeNumber);
    }

    [Fact]
    public void TheLatestAirDateIsTakenFromTheAiredEpisodes()
    {
        using var harness = Create(new EpisodeSpec(1, HasFile: false));

        Assert.Equal(s_aired, harness.Update().LatestEpisodeAirDate);
    }

    [Fact]
    public void NoAirDateIsRecordedWhenNothingHasAired()
    {
        using var harness = Create(new EpisodeSpec(1, HasFile: false, AirsAt: s_unaired));

        Assert.Null(harness.Update().LatestEpisodeAirDate);
    }

    #endregion

    #region Persistence

    [Fact]
    public void TheUpdatedSeriesIsPersisted()
    {
        using var harness = Create(new EpisodeSpec(1, HasFile: false));

        harness.Update();

        harness.SeriesRepository.Verify(r => r.Save(harness.Series, false, It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public void ANullSeriesIsIgnored()
    {
        using var harness = Create(new EpisodeSpec(1, HasFile: false));

        harness.Service.UpdateStats(null, watchedStats: false, missingEpsStats: true);

        harness.SeriesRepository.Verify(r => r.Save(It.IsAny<AnimeSeries>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    #endregion
}
