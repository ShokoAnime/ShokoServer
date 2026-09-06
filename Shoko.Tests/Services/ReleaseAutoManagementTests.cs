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
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="ReleaseAutoManagementService.ComputeRedundantPlaces"/>, which decides which of
/// a user's files get deleted when a better release of the same episodes is present.
/// </summary>
/// <remarks>
/// This is the most destructive decision the server makes on its own, and it had no tests. The
/// method returns the list rather than acting on it, so every rule can be checked without deleting
/// anything. The real <see cref="ReleaseComparisonService"/> is used rather than a stub, so these
/// exercise the actual ranking and redundancy rules.
/// </remarks>
public class ReleaseAutoManagementTests
{
    private const int AnimeID = 100;

    private static readonly DateTime s_past = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public ReleaseAutoManagementService Service { get; }

        public AnimeSeries Series { get; } = new() { AnimeSeriesID = 1, AniDB_ID = AnimeID };

        public Dictionary<int, VideoLocal> VideoLookup { get; } = [];

        public Harness(ReleaseComparisonPreferences? preferences, bool seriesIsAiring, IEnumerable<(int placeId, int videoId, int episodeNumber)>? files)
        {
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(s => s.GetSettings(It.IsAny<bool>()))
                .Returns(new ServerSettings { ReleaseComparisonPreferences = preferences ?? new ReleaseComparisonPreferences() });

            var episodes = new List<AniDB_Episode>();
            var crossRefs = new List<CrossRef_File_Episode>();
            var videos = new List<VideoLocal>();
            foreach (var (placeId, videoId, episodeNumber) in files ?? [])
            {
                var hash = $"hash-{videoId}";
                if (!VideoLookup.ContainsKey(videoId))
                {
                    var video = new VideoLocal { VideoLocalID = videoId, Hash = hash, FileSize = 1000 + videoId };
                    videos.Add(video);
                    VideoLookup[videoId] = video;
                }

                if (episodes.All(e => e.EpisodeNumber != episodeNumber))
                    episodes.Add(new AniDB_Episode
                    {
                        AniDB_EpisodeID = episodeNumber,
                        EpisodeID = episodeNumber,
                        AnimeID = AnimeID,
                        EpisodeNumber = episodeNumber,
                        EpisodeType = EpisodeType.Episode,
                    });

                crossRefs.Add(new CrossRef_File_Episode
                {
                    CrossRef_File_EpisodeID = placeId,
                    Hash = hash,
                    AnimeID = AnimeID,
                    EpisodeID = episodeNumber,
                    Percentage = 100,
                });
            }

            var videoRepository = CachedRepo.Build<VideoLocalRepository, int, VideoLocal>(v => v.VideoLocalID, videos);
            var episodeRepository = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(e => e.AniDB_EpisodeID, episodes);
            var crossRefRepository = CachedRepo.Build<CrossRef_File_EpisodeRepository, int, CrossRef_File_Episode>(x => x.CrossRef_File_EpisodeID, crossRefs);
            var releaseInfoRepository = CachedRepo.Build<StoredReleaseInfoRepository, int, StoredReleaseInfo>(r => r.StoredReleaseInfoID, []);
            var animeRepository = CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(a => a.AniDB_AnimeID,
                [new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = AnimeID, MainTitle = "Test", EndDate = seriesIsAiring ? null : new PartialDateOnly(s_past.Year, s_past.Month, s_past.Day) }]);

            Service = new ReleaseAutoManagementService(
                settings.Object,
                new VideoReleaseGroupingService(videoRepository, episodeRepository, releaseInfoRepository, crossRefRepository),
                new ReleaseComparisonService(settings.Object, null!),
                videoRepository,
                videoLocalPlaces: null!,
                crossRefRepository,
                animeSeries: null!,
                animeRepository,
                videoService: null!,
                NullLogger<ReleaseAutoManagementService>.Instance);
        }
    }

    private static VideoLocal_Place Place(int id, int videoId)
        => new() { ID = id, VideoID = videoId, ManagedFolderID = 1, RelativePath = $"file-{id}.mkv" };

    private static VideoReleaseCandidate Candidate(
        string key,
        IReadOnlyList<VideoLocal_Place> places,
        IEnumerable<int> episodeNumbers,
        bool hasReleaseInfo = true,
        bool isMixed = false,
        bool isCorrupted = false,
        bool isChapteredMixed = false,
        bool isCensoredMixed = false,
        bool isCreditlessMixed = false)
        => new()
        {
            Key = key,
            Places = places,
            EpisodeCoverage = episodeNumbers.Select(n => (EpisodeType.Episode, n)).ToHashSet(),
            HasReleaseInfo = hasReleaseInfo,
            IsMixed = isMixed,
            IsCorrupted = isCorrupted,
            IsChapteredMixed = isChapteredMixed,
            IsCensoredMixed = isCensoredMixed,
            IsCreditlessMixed = isCreditlessMixed,
        };

    /// <summary>Two candidates covering episode 1, ranked primary first.</summary>
    private static Harness TwoCandidates(ReleaseComparisonPreferences? preferences = null, bool seriesIsAiring = false)
        => new(preferences, seriesIsAiring, [(1, 1, 1), (2, 2, 1)]);

    #region Nothing to do

    [Fact]
    public void NothingIsDeletedWhenThereIsOnlyOneCandidate()
    {
        var harness = TwoCandidates();
        var only = Candidate("primary", [Place(1, 1)], [1]);

        Assert.Empty(harness.Service.ComputeRedundantPlaces(harness.Series, [only], harness.VideoLookup));
    }

    [Fact]
    public void NothingIsDeletedWhenThereAreNoCandidates()
    {
        var harness = TwoCandidates();

        Assert.Empty(harness.Service.ComputeRedundantPlaces(harness.Series, [], harness.VideoLookup));
    }

    [Fact]
    public void ASecondaryCoveringEpisodesThePrimaryDoesNotIsKept()
    {
        var harness = new Harness(null, seriesIsAiring: false, [(1, 1, 1), (2, 2, 2)]);
        var primary = Candidate("primary", [Place(1, 1)], [1]);
        var secondary = Candidate("secondary", [Place(2, 2)], [2]);

        // The primary does not provide episode 2, so nothing about the secondary is redundant.
        Assert.Empty(harness.Service.ComputeRedundantPlaces(harness.Series, [primary, secondary], harness.VideoLookup));
    }

    #endregion

    #region The primary is never deleted

    [Fact]
    public void APlaceBelongingToThePrimaryIsNeverDeleted()
    {
        var harness = TwoCandidates();
        var shared = Place(1, 1);
        var primary = Candidate("primary", [shared], [1]);
        // The same physical file also appears in a secondary gap-fill candidate.
        var secondary = Candidate("secondary", [shared, Place(2, 2)], [1]);

        var redundant = harness.Service.ComputeRedundantPlaces(harness.Series, [primary, secondary], harness.VideoLookup);

        Assert.DoesNotContain(redundant, place => place.ID == shared.ID);
    }

    [Fact]
    public void AFileSharedByBothCandidatesSurvivesWhileTheRestOfTheSecondaryGoes()
    {
        var harness = new Harness(null, seriesIsAiring: false, [(1, 1, 1), (2, 2, 1)]);
        var shared = Place(1, 1);
        var primary = Candidate("primary", [shared], [1]);
        var secondary = Candidate("secondary", [shared, Place(2, 2)], [1]);

        var redundant = harness.Service.ComputeRedundantPlaces(harness.Series, [primary, secondary], harness.VideoLookup);

        Assert.Equal([2], redundant.Select(p => p.ID).Order());
    }

    #endregion

    #region The eligibility gate

    [Theory]
    [InlineData(false, false, false, false, false, true)]
    [InlineData(true, false, false, false, false, false)]
    [InlineData(false, true, false, false, false, false)]
    [InlineData(false, false, true, false, false, false)]
    [InlineData(false, false, false, true, false, false)]
    [InlineData(false, false, false, false, true, false)]
    public void AnIneligiblePrimaryDeletesNothing(
        bool isMixed, bool isCorrupted, bool isChapteredMixed, bool isCensoredMixed, bool isCreditlessMixed, bool expectDeletion)
    {
        var harness = TwoCandidates();
        var primary = Candidate("primary", [Place(1, 1)], [1],
            isMixed: isMixed, isCorrupted: isCorrupted,
            isChapteredMixed: isChapteredMixed, isCensoredMixed: isCensoredMixed, isCreditlessMixed: isCreditlessMixed);
        var secondary = Candidate("secondary", [Place(2, 2)], [1]);

        var redundant = harness.Service.ComputeRedundantPlaces(harness.Series, [primary, secondary], harness.VideoLookup);

        // A primary that cannot be trusted to stand in for the others must not cause deletions.
        Assert.Equal(expectDeletion, redundant.Count > 0);
    }

    [Fact]
    public void APrimaryWithoutReleaseInfoDeletesNothing()
    {
        var harness = TwoCandidates();
        var primary = Candidate("primary", [Place(1, 1)], [1], hasReleaseInfo: false);
        var secondary = Candidate("secondary", [Place(2, 2)], [1]);

        Assert.Empty(harness.Service.ComputeRedundantPlaces(harness.Series, [primary, secondary], harness.VideoLookup));
    }

    [Fact]
    public void TheGateCanOnlyBeBypassedDeliberately()
    {
        var harness = TwoCandidates();
        var primary = Candidate("primary", [Place(1, 1)], [1], hasReleaseInfo: false);
        var secondary = Candidate("secondary", [Place(2, 2)], [1]);

        // Reserved for a primary the user picked by hand; unattended paths must never pass this.
        var redundant = harness.Service.ComputeRedundantPlaces(
            harness.Series, [primary, secondary], harness.VideoLookup, bypassEligibilityGate: true);

        Assert.Equal([2], redundant.Select(p => p.ID));
    }

    #endregion

    #region Redundancy

    [Fact]
    public void ASecondaryFullyCoveredByThePrimaryIsRedundant()
    {
        var harness = TwoCandidates();
        var primary = Candidate("primary", [Place(1, 1)], [1]);
        var secondary = Candidate("secondary", [Place(2, 2)], [1]);

        var redundant = harness.Service.ComputeRedundantPlaces(harness.Series, [primary, secondary], harness.VideoLookup);

        Assert.Equal([2], redundant.Select(p => p.ID));
    }

    [Fact]
    public void APlaceAppearingInTwoSecondariesIsOnlyListedOnce()
    {
        var harness = TwoCandidates();
        var duplicated = Place(2, 2);
        var primary = Candidate("primary", [Place(1, 1)], [1]);
        var first = Candidate("second", [duplicated], [1]);
        var second = Candidate("third", [duplicated], [1]);

        var redundant = harness.Service.ComputeRedundantPlaces(harness.Series, [primary, first, second], harness.VideoLookup);

        // Deleting the same file twice would fail the second time round.
        Assert.Single(redundant);
    }

    #endregion

    #region Per-file mode

    [Fact]
    public void AnAiringSeriesUsesPerFileDeletionWhenConfigured()
    {
        var preferences = new ReleaseComparisonPreferences { PerFileDeletionForAiringSeries = true };
        var harness = new Harness(preferences, seriesIsAiring: true, [(1, 1, 1), (2, 2, 1), (3, 3, 2)]);
        var primary = Candidate("primary", [Place(1, 1)], [1]);
        // One file duplicates episode 1, the other adds episode 2 the primary does not have.
        var secondary = Candidate("secondary", [Place(2, 2), Place(3, 3)], [1, 2]);

        var redundant = harness.Service.ComputeRedundantPlaces(harness.Series, [primary, secondary], harness.VideoLookup);

        // Per file, only the duplicate goes; the file carrying episode 2 is kept.
        Assert.Equal([2], redundant.Select(p => p.ID));
    }

    [Fact]
    public void AFinishedSeriesComparesWholeCandidatesInstead()
    {
        var preferences = new ReleaseComparisonPreferences { PerFileDeletionForAiringSeries = true };
        var harness = new Harness(preferences, seriesIsAiring: false, [(1, 1, 1), (2, 2, 1), (3, 3, 2)]);
        var primary = Candidate("primary", [Place(1, 1)], [1]);
        var secondary = Candidate("secondary", [Place(2, 2), Place(3, 3)], [1, 2]);

        var redundant = harness.Service.ComputeRedundantPlaces(harness.Series, [primary, secondary], harness.VideoLookup);

        // The secondary as a whole is not covered by the primary, so none of it is removed.
        Assert.Empty(redundant);
    }

    [Fact]
    public void AFileWithUnknownEpisodeCoverageIsKept()
    {
        var preferences = new ReleaseComparisonPreferences { PerFileDeletionForAiringSeries = true };
        // Place 9 has no cross-reference, so its coverage cannot be resolved.
        var harness = new Harness(preferences, seriesIsAiring: true, [(1, 1, 1)]);
        var primary = Candidate("primary", [Place(1, 1)], [1]);
        var secondary = Candidate("secondary", [Place(9, 9)], [1]);

        var redundant = harness.Service.ComputeRedundantPlaces(harness.Series, [primary, secondary], harness.VideoLookup);

        // Never delete a file we cannot prove is covered elsewhere.
        Assert.Empty(redundant);
    }

    [Fact]
    public void IsSeriesAiringFollowsTheEndDate()
    {
        Assert.True(new Harness(null, seriesIsAiring: true, null).Service.IsSeriesAiring(new AnimeSeries { AniDB_ID = AnimeID }));
        Assert.False(new Harness(null, seriesIsAiring: false, null).Service.IsSeriesAiring(new AnimeSeries { AniDB_ID = AnimeID }));
    }

    [Fact]
    public void AnUnknownSeriesIsNotTreatedAsAiring()
        => Assert.False(new Harness(null, seriesIsAiring: true, null).Service.IsSeriesAiring(new AnimeSeries { AniDB_ID = 999 }));

    #endregion
}
