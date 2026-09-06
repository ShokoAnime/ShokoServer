using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the playlist DSL parsed by <see cref="GeneratedPlaylistService.TryParsePlaylist"/>. It is
/// user-supplied text arriving from the v3 API, so how it rejects bad input matters as much as how
/// it accepts good input.
/// </summary>
/// <remarks>
/// Only the paths that reject an entry are covered here. Once an entry parses, the service builds
/// the actual playlist, which needs the full service graph behind it — a separate exercise.
/// </remarks>
[Collection(nameof(RepoFactoryCollection))]
public class PlaylistParsingTests
{
    private const int GroupID = 5;

    private sealed class Harness : System.IDisposable
    {
        public GeneratedPlaylistService Service { get; }

        private readonly RepoFactoryScope _scope;

        public Harness()
        {
            var groups = CachedRepo.Build<AnimeGroupRepository, int, AnimeGroup>(
                g => g.AnimeGroupID, [new AnimeGroup { AnimeGroupID = GroupID, GroupName = "Group" }]);
            var series = CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(s => s.AnimeSeriesID, []);
            var episodes = CachedRepo.Build<AnimeEpisodeRepository, int, AnimeEpisode>(e => e.AnimeEpisodeID, []);
            var videos = CachedRepo.Build<VideoLocalRepository, int, VideoLocal>(v => v.VideoLocalID, []);

            _scope = new RepoFactoryScope().Set(groups).Set(series).Set(episodes).Set(videos);

            Service = new GeneratedPlaylistService(
                systemService: null!, imageManager: null!, contextAccessor: null!,
                groupRepository: groups, animeSeriesService: null!, seriesRepository: series,
                episodeRepository: episodes, videoRepository: videos, authTokensRepository: null!);
        }

        public (bool Valid, string Errors, int Entries, string Keys) Parse(params string[] items)
        {
            var state = new ModelStateDictionary();
            var valid = Service.TryParsePlaylist(items, out var playlist, state);
            var errors = string.Join(" | ", state.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            var keys = string.Join(",", state.Where(entry => entry.Value?.Errors.Count > 0).Select(entry => entry.Key));
            return (valid, errors, playlist.Count, keys);
        }

        public void Dispose() => _scope.Dispose();
    }

    #region Nothing to play

    [Fact]
    public void AnEmptyPlaylistProducesNothing()
    {
        using var harness = new Harness();

        var (valid, _, entries, _) = harness.Parse();

        Assert.True(valid);
        Assert.Equal(0, entries);
    }

    [Fact]
    public void AnEmptyEntryIsSkippedWithoutDisturbingItsNeighbours()
    {
        using var harness = new Harness();

        // The skip itself is not observable — with the guard removed an empty entry produces
        // nothing and is discarded further down regardless. What is observable is that it still
        // consumes a position, so the error is attributed to the right entry.
        var (valid, errors, _, keys) = harness.Parse("", "g999", "");

        Assert.False(valid);
        Assert.Equal("Unknown group ID \"g999\".", errors);
        Assert.Equal("playlist[1]", keys);
    }

    #endregion

    #region Rejected entries

    [Fact]
    public void AnUnknownGroupIsRejected()
    {
        using var harness = new Harness();

        var (valid, errors, _, _) = harness.Parse("g999");

        Assert.False(valid);
        Assert.Contains("Unknown group ID", errors);
    }

    [Theory]
    [InlineData("gabc")]
    [InlineData("g0")]
    [InlineData("g-1")]
    public void AGroupIdThatIsNotAPositiveNumberIsRejected(string item)
    {
        using var harness = new Harness();

        var (valid, errors, _, _) = harness.Parse(item);

        Assert.False(valid);
        Assert.Contains("Invalid group ID", errors);
    }

    [Theory]
    [InlineData("rabc")]
    [InlineData("r0")]
    public void AReleaseGroupIdThatIsNotAPositiveNumberIsRejected(string releaseItem)
    {
        using var harness = new Harness();

        var (valid, errors, _, _) = harness.Parse($"g{GroupID} {releaseItem}");

        Assert.False(valid);
        Assert.Contains("Invalid release group ID", errors);
    }

    [Fact]
    public void AGroupEntryWithMoreThanAReleaseGroupIsRejected()
    {
        using var harness = new Harness();

        var (valid, errors, _, _) = harness.Parse($"g{GroupID} r7 e9");

        Assert.False(valid);
        Assert.Contains("Invalid item", errors);
    }

    [Fact]
    public void AGroupEntryWithATrailingWordIsRejected()
    {
        using var harness = new Harness();

        var (valid, errors, _, _) = harness.Parse($"g{GroupID} nonsense");

        Assert.False(valid);
        Assert.Contains("Invalid item", errors);
    }

    #endregion

    #region Documented extras

    /// <summary>
    /// Every item below is copied from the remarks on
    /// <see cref="GeneratedPlaylistService.ParsePlaylist"/>, which document them as valid. All of
    /// them are currently rejected: the entry is split on '+' before the extras suffix is looked
    /// for, so the suffix survives as a separate sub-item and trips the "one sub-item only" check.
    /// `recursive` fares worst — it begins with 'r', so it is taken for a release group ID.
    /// </summary>
    /// <remarks>
    /// Written against the intended behaviour, so it turns green when the split is corrected.
    /// Measured: with the fix the parser does accept all of these. The assertion also needs the
    /// playlist to build afterwards, which reaches `GetUser` and so needs
    /// `ISystemService.StaticServices` — nothing in this assembly sets that today, so expect to
    /// stub it when un-skipping.
    /// </remarks>
    [Theory(Skip = "Possible bug - Needs investigation")]
    [InlineData("g5+recursive")]
    [InlineData("g5+includeAllSeries")]
    [InlineData("g5+onlyUnwatched")]
    [InlineData("g5+includeAllSeries-onlyUnwatched")]
    [InlineData("a123+onlyUnwatched")]
    [InlineData("s456+includeSpecials")]
    public void TheDocumentedExtrasSyntaxIsAccepted(string item)
    {
        using var harness = new Harness();

        Assert.True(harness.Parse(item).Valid);
    }

    #endregion
}