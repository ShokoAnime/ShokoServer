using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

using Kind = Shoko.Server.Services.MovieTextRules.PlaceholderKind;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="MovieTextRules"/>: which AniDB episode titles are stand-in names, and which film
/// or collection speaks for a whole anime. The shapes are taken from anime whose episodes share one
/// film on TMDB, with each episode's AniDB English title as AniDB gives it.
/// </summary>
public class MovieTextRulesTests
{
    #region Placeholders

    [Theory]
    [InlineData("Complete Movie", nameof(Kind.Whole), null, null)]
    [InlineData("OVA", nameof(Kind.Whole), null, null)]
    [InlineData("Movie", nameof(Kind.Whole), null, null)]
    [InlineData("complete ova", nameof(Kind.Whole), null, null)]
    [InlineData("TV Special", nameof(Kind.Whole), null, null)]
    [InlineData("Complete Movie (Decide Version)", nameof(Kind.Whole), null, "Decide Version")]
    [InlineData("Complete Movie (Glory Version)", nameof(Kind.Whole), null, "Glory Version")]
    [InlineData("Part 1 of 2", nameof(Kind.Part), "Part 1 of 2", null)]
    [InlineData("Part 3 of 4", nameof(Kind.Part), "Part 3 of 4", null)]
    [InlineData("Part I", nameof(Kind.Part), "Part I", null)]
    [InlineData("Part II", nameof(Kind.Part), "Part II", null)]
    [InlineData("Complete Movie (Part 1)", nameof(Kind.Part), "Part 1", null)]
    [InlineData("Episode 1", nameof(Kind.Numbered), "Episode 1", null)]
    [InlineData("Episode 10", nameof(Kind.Numbered), "Episode 10", null)]
    [InlineData("Volume 1", nameof(Kind.Numbered), "Volume 1", null)]
    [InlineData("Movie 2", nameof(Kind.Numbered), "Movie 2", null)]
    [InlineData("OVA 1", nameof(Kind.Numbered), "OVA 1", null)]
    [InlineData("Episode 1 (Part 2)", nameof(Kind.Numbered), "Episode 1", "Part 2")]
    [InlineData("  episode   2 ", nameof(Kind.Numbered), "episode 2", null)]
    public void StandInNames_AreReadWithTheirLabels(string title, string kind, string? label, string? suffix)
        => Assert.Equal(new MovieTextRules.Placeholder(Enum.Parse<Kind>(kind), label, suffix), MovieTextRules.ParsePlaceholder(title));

    [Theory]
    [InlineData("Magnetic Rose")]
    [InlineData("Death")]
    [InlineData("Rebirth")]
    [InlineData("5 Centimeters per Second")]
    [InlineData("Pilot 2 (1987)")]
    [InlineData("Face A")]
    [InlineData("Side: Galo")]
    [InlineData("Genius Party")]
    [InlineData("Opening")]
    [InlineData("Pokemon Ranger: Guardian Signs")]
    [InlineData("No Coincidences in This Summer Break (Part 1)")]
    [InlineData("About Kaguya Shinomiya, Part 4 / Kaguya Wants to Be Noticed (Ice) / Kaguya Wants to Forgive (Ice)")]
    [InlineData("Death Trap / Charge! Adventures of the Okutama Explorers")]
    [InlineData("Part")]
    [InlineData("Part ix")]
    [InlineData("Episode One")]
    [InlineData("")]
    [InlineData(null)]
    public void RealNames_AreNoStandIns(string? title)
        => Assert.Null(MovieTextRules.ParsePlaceholder(title));

    [Theory]
    [InlineData("Part 1 of 2", "Vampire Hunter D (Part 1 of 2)")]
    [InlineData("Part II", "Vampire Hunter D (Part II)")]
    [InlineData("Episode 3", "Vampire Hunter D (Episode 3)")]
    [InlineData("Volume 1", "Vampire Hunter D (Volume 1)")]
    [InlineData("Complete Movie", "Vampire Hunter D")]
    [InlineData("OVA", "Vampire Hunter D")]
    [InlineData("Complete Movie (Decide Version)", "Vampire Hunter D (Decide Version)")]
    [InlineData("Complete Movie (Part 2)", "Vampire Hunter D (Part 2)")]
    [InlineData("Episode 1 (Part 2)", "Vampire Hunter D (Episode 1) (Part 2)")]
    public void AFilmTitle_KeepsTheStandInsLabel(string anidbTitle, string expected)
        => Assert.Equal(expected, MovieTextRules.LabelFilmTitle("Vampire Hunter D", MovieTextRules.ParsePlaceholder(anidbTitle)!));

    #endregion

    #region Series

    private static readonly MetadataGuid _filmA = Film("1");

    private static readonly MetadataGuid _filmB = Film("2");

    private static readonly MetadataGuid _filmC = Film("3");

    private static readonly MetadataGuid _franchise = new(TestSources.Plugin, MetadataEntityType.Collection, "10");

    private static readonly MetadataGuid _saga = new(TestSources.Plugin, MetadataEntityType.Collection, "11");

    private static MetadataGuid Film(string id)
        => new(TestSources.Plugin, MetadataEntityType.Movie, id);

    private static MovieTextRules.EpisodeFilms Episode(int id, string? title, params MetadataGuid[] films)
        => new(id, EpisodeType.Episode, title, films);

    private static MovieTextRules.EpisodeFilms Special(int id, string? title, params MetadataGuid[] films)
        => new(id, EpisodeType.Special, title, films);

    private static MovieTextRules.EpisodeFilms Other(int id, string? title, params MetadataGuid[] films)
        => new(id, EpisodeType.Other, title, films);

    private static MetadataGuid? Choose(Dictionary<MetadataGuid, MetadataGuid[]>? collections, params MovieTextRules.EpisodeFilms[] episodes)
        => MovieTextRules.ChooseSeriesEntry(episodes, film => collections?.GetValueOrDefault(film) ?? []);

    [Fact]
    public void EveryNormalEpisodeLinkedToFilmsOfOneCollection_IsThatCollection()
    {
        var collections = new Dictionary<MetadataGuid, MetadataGuid[]> { [_filmA] = [_franchise], [_filmB] = [_franchise, _saga], [_filmC] = [_franchise] };

        Assert.Equal(_franchise, Choose(collections, Episode(1, "Part One", _filmA), Episode(2, "Part Two", _filmB), Episode(3, "Part Three", _filmC)));
    }

    [Fact]
    public void FilmsSharingTwoCollections_SayNothing()
    {
        var collections = new Dictionary<MetadataGuid, MetadataGuid[]> { [_filmA] = [_franchise, _saga], [_filmB] = [_saga, _franchise] };

        Assert.Null(Choose(collections, Episode(1, "First", _filmA), Episode(2, "Second", _filmB)));
    }

    [Fact]
    public void FilmsOfDifferentCollections_SayNothing()
    {
        var collections = new Dictionary<MetadataGuid, MetadataGuid[]> { [_filmA] = [_franchise], [_filmB] = [_saga] };

        Assert.Null(Choose(collections, Episode(1, "First", _filmA), Episode(2, "Second", _filmB)));
    }

    [Fact]
    public void FilmsInNoCollection_SayNothing()
        => Assert.Null(Choose(null, Episode(1, "Magnetic Rose", _filmA), Episode(2, "Stink Bomb", _filmB)));

    [Fact]
    public void ANormalEpisodeWithoutAFilm_SaysNothing()
        => Assert.Null(Choose(null, Episode(1, "Episode 1", _filmA), Episode(2, "Episode 2")));

    [Fact]
    public void SpecialsAndOtherEpisodes_AreLeftOut()
        => Assert.Equal(_filmA, Choose(null, Episode(1, "Complete Movie", _filmA), Special(2, "Recap", _filmB), Other(3, "Pilot"), Special(4, "Part 1 of 2", _filmC)));

    [Fact]
    public void FilmsLinkedOnlyToSpecials_SayNothing()
        => Assert.Null(Choose(null, Episode(1, "Episode 1"), Episode(2, "Episode 2"), Special(3, "TV Special", _filmA)));

    [Fact]
    public void AnAnimeWithoutNormalEpisodes_SaysNothing()
        => Assert.Null(Choose(null, Special(1, "Pilot 2 (1987)", _filmA), Special(2, "Pilot (1980)", _filmA)));

    [Fact]
    public void AnEpisodeLinkedToTwoFilmsOfOneCollection_IsThatCollection()
    {
        var collections = new Dictionary<MetadataGuid, MetadataGuid[]> { [_filmA] = [_franchise], [_filmB] = [_franchise] };

        Assert.Equal(_franchise, Choose(collections, Episode(1, "The Whole Story", _filmA, _filmB)));
    }

    #endregion

    #region Series | Complete and parts

    [Fact]
    public void TheWholeEpisodesFilm_WinsOverConflictingParts()
    {
        var collections = new Dictionary<MetadataGuid, MetadataGuid[]> { [_filmB] = [_franchise], [_filmC] = [_saga] };

        Assert.Equal(_filmA, Choose(collections,
            Episode(1, "Complete Movie", _filmA),
            Episode(2, "Part 1 of 2", _filmB),
            Episode(3, "Part 2 of 2", _filmC)));
    }

    [Fact]
    public void TheWholeEpisodesFilm_IsUsedWithoutItsCollection()
    {
        var collections = new Dictionary<MetadataGuid, MetadataGuid[]> { [_filmA] = [_franchise] };

        Assert.Equal(_filmA, Choose(collections, Episode(1, "Complete Movie", _filmA), Episode(2, "Part 1 of 2"), Episode(3, "Part 2 of 2")));
    }

    [Fact]
    public void AnOvaStandInWithParts_IsReadAsTheWholeFilm()
        => Assert.Equal(_filmA, Choose(null, Episode(1, "OVA", _filmA), Episode(2, "Part 1 of 2", _filmA), Episode(3, "Part 2 of 2", _filmA)));

    [Fact]
    public void TwoVersionsOfTheWholeFilm_AreThatFilm()
        => Assert.Equal(_filmA, Choose(null, Episode(1, "Complete Movie (Decide Version)", _filmA), Episode(2, "Complete Movie (Glory Version)", _filmA)));

    [Fact]
    public void TwoWholeEpisodesOfTwoFilms_SayNothing()
        => Assert.Null(Choose(null, Episode(1, "Complete Movie (Decide Version)", _filmA), Episode(2, "Complete Movie (Glory Version)", _filmB)));

    [Fact]
    public void UnlinkedPartsAndAnUnlinkedWholeEpisode_AreLeftOut()
        => Assert.Equal(_filmA, Choose(null,
            Episode(1, "Complete Movie"),
            Episode(2, "Part 1 of 2", _filmA),
            Episode(3, "Part 2 of 2", _filmA),
            Episode(4, "Part 1 of 3"),
            Episode(5, "Part 2 of 3"),
            Episode(6, "Part 3 of 3")));

    [Fact]
    public void PartsLinkedToFilmsOfOneCollection_AreThatCollection()
    {
        var collections = new Dictionary<MetadataGuid, MetadataGuid[]> { [_filmA] = [_franchise], [_filmB] = [_franchise] };

        Assert.Equal(_franchise, Choose(collections, Episode(1, "Part I", _filmA), Episode(2, "Part II", _filmB)));
    }

    [Fact]
    public void PartsLinkedToUnrelatedFilms_SayNothing()
        => Assert.Null(Choose(null, Episode(1, "Complete Movie"), Episode(2, "Part 1 of 2", _filmA), Episode(3, "Part 2 of 2", _filmB)));

    [Fact]
    public void NoLinkedWholeEpisodeOrPart_SaysNothing()
        => Assert.Null(Choose(null, Episode(1, "Complete Movie"), Episode(2, "Part 1 of 2"), Other(3, "Making Of", _filmA)));

    [Fact]
    public void AWholeEpisodeBesideNamedOtherEpisodes_IsThatFilm()
        => Assert.Equal(_filmA, Choose(null,
            Episode(1, "Complete Movie", _filmA),
            Other(2, "Flying Memory", _filmA),
            Other(3, "Grown-Up Wannabe", _filmA)));

    [Fact]
    public void PartsOfAnotherType_BelongToTheWholeFilm()
        => Assert.Equal(_filmA, Choose(null, Episode(1, "Complete Movie"), Other(2, "Part 1 of 2", _filmA), Other(3, "Part 2 of 2", _filmA)));

    [Fact]
    public void PartsWithoutAWholeEpisode_MustAllBeLinked()
        => Assert.Null(Choose(null, Episode(1, "Part 1 of 3", _filmA), Episode(2, "Part 2 of 3"), Episode(3, "Part 3 of 3")));

    [Fact]
    public void AWholeSpecialsFilm_DoesNotConflictWithTheNormalWholeEpisodesFilm()
        => Assert.Equal(_filmA, Choose(null, Episode(1, "Complete Movie", _filmA), Special(2, "TV Special", _filmB), Other(3, "OVA", _filmC)));

    [Fact]
    public void AWholeSpecialsFilm_NeverNamesTheAnime()
        => Assert.Null(Choose(null, Episode(1, "Complete Movie"), Special(2, "TV Special", _filmB)));

    [Fact]
    public void AWholeSpecialsFilm_LeavesTheLinkedPartsToSpeak()
        => Assert.Equal(_filmA, Choose(null,
            Episode(1, "Complete Movie"),
            Episode(2, "Part 1 of 2", _filmA),
            Episode(3, "Part 2 of 2"),
            Special(4, "TV Special", _filmB)));

    [Fact]
    public void AWholeSpecialOfANamedSeries_IsNoFilmEntry()
        => Assert.Null(Choose(null, Episode(1, "The Beginning"), Episode(2, "The End"), Special(3, "TV Special", _filmA)));

    [Fact]
    public void NumberedStandIns_FollowTheNormalRule()
        => Assert.Equal(_filmA, Choose(null,
            Episode(1, "Episode 1", _filmA),
            Episode(2, "Episode 2", _filmA),
            Other(3, "Pokemon Ranger: Guardian Signs", _filmA)));

    #endregion

    #region Series | Every shape

    /// <summary>
    /// Anime with one film shared by several episodes, as the English titles of those episodes.
    /// </summary>
    public static TheoryData<int, string[]> SharedFilmsWithStandIns => new()
    {
        { 66, ["Complete Movie", "Part 1 of 2", "Part 2 of 2", "Part 1 of 3", "Part 2 of 3", "Part 3 of 3"] },
        { 204, ["OVA", "Part 1 of 2", "Part 2 of 2"] },
        { 567, ["Volume 1", "Volume 2"] },
        { 1570, ["TV Special", "Part 1 of 2", "Part 2 of 2"] },
        { 1755, ["Part I", "Part II"] },
        { 2600, ["OVA 1", "OVA 2"] },
        { 2643, ["Episode 1", "Episode 2"] },
        { 4590, ["Complete Movie", "Part 1 of 4", "Part 2 of 4", "Part 3 of 4", "Part 4 of 4"] },
        { 8513, ["Episode 1", "Episode 2", "Episode 3", "Episode 4", "Episode 5", "Episode 6", "Episode 7", "Episode 8", "Episode 9", "Episode 10"] },
        { 14415, ["Movie 1", "Movie 2"] },
        { 14551, ["Complete Movie (Decide Version)", "Complete Movie (Glory Version)"] },
        { 18039, ["Movie 1", "Movie 2"] },
    };

    /// <summary>
    /// Anime whose episodes sharing one film each have a name of their own: anthologies and named
    /// chapters of one film alike.
    /// </summary>
    public static TheoryData<int, string[]> SharedFilmsWithNames => new()
    {
        { 262, ["Magnetic Rose", "Stink Bomb", "Cannon Fodder"] },
        { 326, ["Death", "Rebirth"] },
        { 2530, ["Pilot 2 (1987)", "Pilot (1980)"] },
        { 2904, ["The Vulgar Family", "Monster Kingdom", "The Vulgar Family, Yet Again", "Rebel Without a Cause"] },
        { 2972, ["Genius Party", "Baby Blue"] },
        { 4568, ["Cherry Blossom", "Cosmonaut", "5 Centimeters per Second"] },
        { 6651, ["The Stowaway", "Apollo and Mikaru", "Cipher"] },
        { 13273, ["SORD", "Soul Speed"] },
        { 17968, ["No Coincidences in This Summer Break (Part 1)", "No Coincidences in This Summer Break (Part 2)"] },
    };

    [Theory]
    [MemberData(nameof(SharedFilmsWithStandIns))]
    [MemberData(nameof(SharedFilmsWithNames))]
    public void EveryEpisodeLinkedToOneFilm_IsThatFilm(int anime, string[] titles)
        => Assert.True(_filmA == Choose(null, [.. titles.Select((title, index) => Episode(index + 1, title, _filmA))]), anime.ToString());

    #endregion
}
