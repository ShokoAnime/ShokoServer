using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="AnimeSeriesService.EpisodeList"/>, which decides whether a multi-part OVA or
/// movie counts as "available". Every part of a split release has to be present before the episode
/// is considered held, and the parts are matched together purely by AniDB's English titles, so a
/// change to the title normalisation silently changes a user's missing-episode counts.
/// </summary>
public class EpisodeListTests
{
    private static AnimeEpisode Episode(bool hidden = false)
        => new() { IsHidden = hidden };

    private static AnimeSeriesService.EpisodeList List(AnimeType type) => new(type);

    #region Series types that are never part-matched

    [Theory]
    [InlineData(AnimeType.TV)]
    [InlineData(AnimeType.Web)]
    [InlineData(AnimeType.TVSpecial)]
    [InlineData(AnimeType.Other)]
    public void NonOvaTypes_KeepEveryEpisodeSeparate(AnimeType type)
    {
        var list = List(type);

        list.Add(Episode(), "part 1 of 2", available: true);
        list.Add(Episode(), "part 2 of 2", available: true);

        // Part matching is deliberately limited to OVA/Movie, so these stay two distinct entries.
        Assert.Equal(2, list.Count);
        Assert.All(list, group => Assert.Equal(string.Empty, group.Single().Match));
    }

    [Fact]
    public void NonOvaTypes_AreAvailableWhenTheFileIsPresent()
    {
        var list = List(AnimeType.TV);

        list.Add(Episode(), "Episode 1", available: true);

        Assert.True(list.Single().Available);
    }

    [Fact]
    public void NonOvaTypes_AreNotAvailableWhenTheFileIsMissing()
    {
        var list = List(AnimeType.TV);

        list.Add(Episode(), "Episode 1", available: false);

        Assert.False(list.Single().Available);
    }

    #endregion

    #region Part detection

    [Theory]
    [InlineData(AnimeType.OVA)]
    [InlineData(AnimeType.Movie)]
    public void PartTitles_AreGroupedTogetherByTheirRemainingName(AnimeType type)
    {
        var list = List(type);

        list.Add(Episode(), "Some Movie part 1 of 2", available: true);
        list.Add(Episode(), "Some Movie part 2 of 2", available: true);

        Assert.Single(list);
        Assert.Equal(2, list.Single().Count);
        Assert.All(list.Single(), part => Assert.Equal("Some Movie", part.Match));
    }

    [Fact]
    public void PartTitles_RecordThePartCount()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "Some Movie part 1 of 3", available: true);

        var part = list.Single().Single();
        Assert.Equal(3, part.PartCount);
        Assert.Equal(AnimeSeriesService.EpisodeList.StatEpisodes.StatEpisode.EpType.Part, part.EpisodeType);
    }

    [Fact]
    public void PartTitles_StripPunctuationWhenMatching()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "Some: Movie! part 1 of 2", available: true);
        list.Add(Episode(), "Some Movie part 2 of 2", available: true);

        // Symbols are removed and runs of whitespace collapsed, so both titles reduce to the same key.
        Assert.Single(list);
    }

    [Fact]
    public void PartTitles_WithGenericNamesCollapseToTheEmptyKey()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "complete movie part 1 of 2", available: true);

        Assert.Equal(string.Empty, list.Single().Single().Match);
    }

    [Fact]
    public void DifferentTitles_AreKeptInSeparateGroups()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "First Movie part 1 of 2", available: true);
        list.Add(Episode(), "Second Movie part 1 of 2", available: true);

        Assert.Equal(2, list.Count);
    }

    #endregion

    #region Whole-episode titles

    [Theory]
    [InlineData("complete movie")]
    [InlineData("movie")]
    [InlineData("ova")]
    public void GenericWholeEpisodeTitles_CollapseToTheEmptyKey(string title)
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), title, available: true);

        var episode = list.Single().Single();
        Assert.Equal(string.Empty, episode.Match);
        Assert.Equal(AnimeSeriesService.EpisodeList.StatEpisodes.StatEpisode.EpType.Complete, episode.EpisodeType);
    }

    [Fact]
    public void GenericWholeEpisodeTitles_GroupWithEachOther()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "complete movie", available: true);
        list.Add(Episode(), "movie", available: false);

        Assert.Single(list);
    }

    [Fact]
    public void NamedWholeEpisodes_KeepTheirNormalisedTitleAsTheKey()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "Some: Movie!", available: true);

        Assert.Equal("Some Movie", list.Single().Single().Match);
    }

    #endregion

    #region Availability

    [Fact]
    public void AllPartsPresent_MakesTheEpisodeAvailable()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "Some Movie part 1 of 2", available: true);
        list.Add(Episode(), "Some Movie part 2 of 2", available: true);

        Assert.True(list.Single().Available);
    }

    [Fact]
    public void AMissingPart_LeavesTheEpisodeUnavailable()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "Some Movie part 1 of 2", available: true);
        list.Add(Episode(), "Some Movie part 2 of 2", available: false);

        Assert.False(list.Single().Available);
    }

    [Fact]
    public void AThreePartEpisodeNeedsEveryPart()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "Some Movie part 1 of 3", available: true);
        list.Add(Episode(), "Some Movie part 2 of 3", available: true);

        Assert.False(list.Single().Available);

        list.Add(Episode(), "Some Movie part 3 of 3", available: true);

        Assert.True(list.Single().Available);
    }

    [Fact]
    public void ACompleteReleaseMakesTheEpisodeAvailableEvenWithMissingParts()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "Some Movie part 1 of 2", available: false);
        list.Add(Episode(), "Some Movie", available: true);

        // A single complete file covers the episode regardless of the part releases around it.
        Assert.True(list.Single().Available);
    }

    #endregion

    #region AniDB's stand-in titles

    [Theory]
    [InlineData("Complete Movie")]
    [InlineData("OVA")]
    [InlineData("TV Special")]
    public void AWholeStandInAndItsParts_AreOneGroup(string whole)
    {
        var list = List(AnimeType.Movie);

        list.Add(Episode(), whole, available: false);
        list.Add(Episode(), "Part 1 of 2", available: true);
        list.Add(Episode(), "Part 2 of 2", available: true);
        list.Add(Episode(), "Part 1 of 3", available: false);
        list.Add(Episode(), "Part 2 of 3", available: false);
        list.Add(Episode(), "Part 3 of 3", available: false);

        // Holding one full split covers the film, as holding the whole of it would.
        Assert.Single(list);
        Assert.True(list.Single().Available);
    }

    [Fact]
    public void TwoVersionsOfTheWholeFilm_AreOneGroup()
    {
        var list = List(AnimeType.Movie);

        list.Add(Episode(), "Complete Movie (Decide Version)", available: true);
        list.Add(Episode(), "Complete Movie (Glory Version)", available: false);

        Assert.Single(list);
        Assert.True(list.Single().Available);
    }

    [Fact]
    public void AFilmTitleWithAPartLabel_GroupsWithTheFilmTitle()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "Vampire Hunter D", available: false);
        list.Add(Episode(), "Vampire Hunter D (Part 1 of 2)", available: true);

        Assert.Single(list);
        Assert.Equal(2, list.Single().Single(part => part.EpisodeType is AnimeSeriesService.EpisodeList.StatEpisodes.StatEpisode.EpType.Part).PartCount);
    }

    [Fact]
    public void NamedEpisodesOfAnAnthology_StaySeparate()
    {
        var list = List(AnimeType.Movie);

        list.Add(Episode(), "Magnetic Rose", available: true);
        list.Add(Episode(), "Stink Bomb", available: false);
        list.Add(Episode(), "Cannon Fodder", available: false);

        Assert.Equal(3, list.Count);
        Assert.Equal(2, list.Count(group => !group.Available));
    }

    #endregion

    #region Hidden

    [Fact]
    public void AGroupIsHiddenWhenAnyOfItsEpisodesIsHidden()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(hidden: false), "Some Movie part 1 of 2", available: true);
        list.Add(Episode(hidden: true), "Some Movie part 2 of 2", available: true);

        Assert.True(list.Single().Hidden);
    }

    [Fact]
    public void AGroupIsNotHiddenWhenNoEpisodeIsHidden()
    {
        var list = List(AnimeType.OVA);

        list.Add(Episode(), "Some Movie", available: true);

        Assert.False(list.Single().Hidden);
    }

    #endregion
}
