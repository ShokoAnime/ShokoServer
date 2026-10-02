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
                g => g.AnimeGroupID, [new AnimeGroup { AnimeGroupID = GroupID }]);
            var series = CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(s => s.AnimeSeriesID, []);
            var episodes = CachedRepo.Build<AnimeEpisodeRepository, int, AnimeEpisode>(e => e.AnimeEpisodeID, []);
            var videos = CachedRepo.Build<VideoLocalRepository, int, VideoLocal>(v => v.VideoLocalID, []);

            _scope = new RepoFactoryScope().Set(groups).Set(series).Set(episodes).Set(videos);

            Service = new GeneratedPlaylistService(
                systemService: null!, imageManager: null!, contextAccessor: null!,
                groupRepository: groups, animeSeriesService: null!, seriesRepository: series,
                episodeRepository: episodes, videoRepository: videos, userService: null!);
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

    #region Documented examples

    private const string Hash = "abc123de0000000000000000000000ff";

    /// <summary>
    /// Every example in the playlist endpoint's docs, against an empty library: an entry that
    /// parses names the entity it could not find, rather than being refused as malformed.
    /// </summary>
    [Theory]
    [InlineData("a123", "Unknown series ID \"a123\".")]
    [InlineData("s456", "Unknown series ID \"s456\".")]
    [InlineData("a123 r789", "Unknown series ID \"a123 r789\".")]
    [InlineData("a123+onlyUnwatched", "Unknown series ID \"a123+onlyUnwatched\".")]
    [InlineData("s456+includeSpecials-includeOthers", "Unknown series ID \"s456+includeSpecials-includeOthers\".")]
    [InlineData("g123", "Unknown group ID \"g123\".")]
    [InlineData("g123+recursive", "Unknown group ID \"g123+recursive\".")]
    [InlineData("g123+includePrequels", "Unknown group ID \"g123+includePrequels\".")]
    [InlineData("g123+includeAllSeries", "Unknown group ID \"g123+includeAllSeries\".")]
    [InlineData("g123+includeAllSeries-onlyUnwatched", "Unknown group ID \"g123+includeAllSeries-onlyUnwatched\".")]
    [InlineData("e98765", "Unknown episode ID \"e98765\" at index 0 at offset 0")]
    [InlineData("E54321", "Unknown episode ID \"E54321\" at index 0 at offset 0")]
    [InlineData("r789 e98765", "Unknown episode ID \"e98765\" at index 0 at offset 1")]
    [InlineData("E54321 f" + Hash, "Unknown episode ID \"E54321\" at index 0 at offset 0 | Unknown hash \"" + Hash + "\" at index 0 at offset 1")]
    [InlineData("42", "Unknown file ID \"42\".")]
    [InlineData(Hash, "Unknown hash \"" + Hash + "\" at index 0 at offset 0")]
    [InlineData(Hash + "-123456", "Unknown hash/size pair \"" + Hash + "-123456\" at index 0 at offset 0")]
    public void EveryDocumentedExampleParses(string item, string error)
    {
        using var harness = new Harness();

        var (valid, errors, _, _) = harness.Parse(item);

        Assert.False(valid);
        Assert.Equal(error, errors);
    }

    [Fact]
    public void TheDocumentedThreeEntryExampleParsesEachEntry()
    {
        using var harness = new Harness();

        var (_, _, _, keys) = harness.Parse("a123", "r789 e98765", "f" + Hash);

        Assert.Equal("playlist[0],playlist[1][1],playlist[2][0]", keys);
    }

    [Fact]
    public void AReleaseGroupAloneIsSkipped()
    {
        using var harness = new Harness();

        var (valid, _, entries, _) = harness.Parse("r789");

        Assert.True(valid);
        Assert.Equal(0, entries);
    }

    /// <summary>
    /// An unencoded <c>+</c> in a query string arrives as a space.
    /// </summary>
    [Theory]
    [InlineData("a123 onlyUnwatched", "Unknown series ID \"a123 onlyUnwatched\".")]
    [InlineData("s1505 onlyUnwatched", "Unknown series ID \"s1505 onlyUnwatched\".")]
    [InlineData("s456 includeSpecials-includeOthers", "Unknown series ID \"s456 includeSpecials-includeOthers\".")]
    [InlineData("g123 recursive", "Unknown group ID \"g123 recursive\".")]
    [InlineData("g123 includeAllSeries-onlyUnwatched", "Unknown group ID \"g123 includeAllSeries-onlyUnwatched\".")]
    [InlineData("a123 onlyUnwatched r789", "Unknown series ID \"a123 onlyUnwatched r789\".")]
    public void ExtrasAfterASpaceParseLikeExtrasAfterAPlus(string item, string error)
    {
        using var harness = new Harness();

        var (valid, errors, _, _) = harness.Parse(item);

        Assert.False(valid);
        Assert.Equal(error, errors);
    }

    [Fact]
    public void ExtrasStayWithTheirSeriesOrGroup()
    {
        Assert.Equal(
            [("g123", ["includeAllSeries", "onlyUnwatched"])],
            GeneratedPlaylistService.SplitSubItems("g123+includeAllSeries-onlyUnwatched").Select(token => (token.Value, token.Extras.ToArray())));
        Assert.Equal(
            [("a123", (string[])[]), ("r789", [])],
            GeneratedPlaylistService.SplitSubItems("a123 r789").Select(token => (token.Value, token.Extras.ToArray())));
    }

    #endregion

    #region Malformed extras

    [Theory]
    [InlineData("a123+bogus")]
    [InlineData("a123+onlyUnwatched-bogus")]
    [InlineData("a123+recursive")]
    [InlineData("s456 includeAllSeries")]
    public void AnExtraTheEntryDoesNotTakeIsRejected(string item)
    {
        using var harness = new Harness();

        var (valid, errors, _, _) = harness.Parse(item);

        Assert.False(valid);
        Assert.Equal($"Invalid item \"{item}\".", errors);
    }

    [Fact]
    public void ASeriesIdThatIsNotANumberIsRejected()
    {
        using var harness = new Harness();

        var (valid, errors, _, _) = harness.Parse("sabc+onlyUnwatched");

        Assert.False(valid);
        Assert.Equal("Invalid series ID \"sabc+onlyUnwatched\".", errors);
    }

    #endregion
}
