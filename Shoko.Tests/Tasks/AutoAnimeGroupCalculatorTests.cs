using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Tasks;
using Xunit;

using AnimeRelation = Shoko.Server.Tasks.AutoAnimeGroupCalculator.AnimeRelation;
using RelationType = Shoko.Server.Tasks.AutoAnimeGroupCalculator.AnimeRelationType;

namespace Shoko.Tests.Tasks;

/// <summary>
/// Covers <see cref="AutoAnimeGroupCalculator"/>, which decides how a user's collection is carved
/// into groups. It is exercised entirely through the public constructor, which takes a prebuilt
/// relation lookup — the database-backed <c>Create</c>/<c>CreateFromServerSettings</c> factories are
/// thin adapters over that same constructor, so none of this needs a database or settings.
/// </summary>
public class AutoAnimeGroupCalculatorTests
{
    #region Helpers

    private sealed record Anime(int Id, string Title = "Alpha", AnimeType Type = AnimeType.TV, PartialDateOnly? AirDate = null);

    /// <summary>
    /// Builds the two directed rows AniDB stores for a single relation, mirroring what the
    /// <c>AniDB_Anime_Relation</c> query in <see cref="AutoAnimeGroupCalculator.Create"/> produces.
    /// </summary>
    private static IEnumerable<AnimeRelation> Link(Anime from, Anime to, RelationType forward, RelationType reverse)
    {
        yield return new AnimeRelation
        {
            FromId = from.Id,
            FromType = from.Type,
            FromMainTitle = from.Title,
            FromAirDate = from.AirDate,
            ToId = to.Id,
            ToType = to.Type,
            ToMainTitle = to.Title,
            ToAirDate = to.AirDate,
            RelationType = forward,
        };
        yield return new AnimeRelation
        {
            FromId = to.Id,
            FromType = to.Type,
            FromMainTitle = to.Title,
            FromAirDate = to.AirDate,
            ToId = from.Id,
            ToType = from.Type,
            ToMainTitle = from.Title,
            ToAirDate = from.AirDate,
            RelationType = reverse,
        };
    }

    private static IEnumerable<AnimeRelation> Sequel(Anime earlier, Anime later)
        => Link(earlier, later, RelationType.Sequel, RelationType.Prequel);

    private static IEnumerable<AnimeRelation> SameSetting(Anime a, Anime b)
        => Link(a, b, RelationType.SameSetting, RelationType.SameSetting);

    private static AutoAnimeGroupCalculator Calc(
        IEnumerable<AnimeRelation> relations,
        AutoGroupExclude exclusions = AutoGroupExclude.None,
        RelationType fuzzyTitleTest = RelationType.None,
        MainAnimeSelectionStrategy strategy = MainAnimeSelectionStrategy.MinAirDate)
        => new(relations.ToLookup(r => r.FromId), exclusions, fuzzyTitleTest, strategy);

    private static void AssertGrouped(AutoAnimeGroupCalculator calculator, int a, int b)
    {
        Assert.Equal(calculator.GetGroupAnimeId(a), calculator.GetGroupAnimeId(b));
        Assert.Contains(b, calculator.GetIdsOfAnimeInSameGroup(a));
    }

    private static void AssertNotGrouped(AutoAnimeGroupCalculator calculator, int a, int b)
    {
        Assert.NotEqual(calculator.GetGroupAnimeId(a), calculator.GetGroupAnimeId(b));
        Assert.DoesNotContain(b, calculator.GetIdsOfAnimeInSameGroup(a));
    }

    private static PartialDateOnly Year(int year) => new(year, 1, 1);

    #endregion

    #region Graph building

    [Fact]
    public void GetGroupAnimeId_ReturnsTheAnimeItself_WhenItHasNoRelations()
    {
        var calculator = Calc([]);

        Assert.Equal(42, calculator.GetGroupAnimeId(42));
    }

    [Fact]
    public void GetIdsOfAnimeInSameGroup_ReturnsOnlyTheAnime_WhenItHasNoRelations()
    {
        var calculator = Calc([]);

        Assert.Equal([42], calculator.GetIdsOfAnimeInSameGroup(42));
    }

    [Fact]
    public void GetGroupAnimeId_GroupsTransitivelyRelatedAnime()
    {
        var first = new Anime(1, AirDate: Year(2000));
        var second = new Anime(2, AirDate: Year(2001));
        var third = new Anime(3, AirDate: Year(2002));
        var calculator = Calc([.. Sequel(first, second), .. Sequel(second, third)]);

        // 1 and 3 share no direct relation; they are only connected through 2.
        AssertGrouped(calculator, 1, 3);
        Assert.Equal([1, 2, 3], calculator.GetIdsOfAnimeInSameGroup(3).Order());
    }

    [Fact]
    public void GetGroupAnimeId_TerminatesOnCyclicRelations()
    {
        var first = new Anime(1, AirDate: Year(2000));
        var second = new Anime(2, AirDate: Year(2001));
        var third = new Anime(3, AirDate: Year(2002));
        var calculator = Calc([.. Sequel(first, second), .. Sequel(second, third), .. Sequel(third, first)]);

        Assert.Equal([1, 2, 3], calculator.GetIdsOfAnimeInSameGroup(1).Order());
    }

    [Fact]
    public void GetGroupAnimeId_HandlesSelfReferentialRelations()
    {
        var self = new Anime(1, AirDate: Year(2000));
        var calculator = Calc(Sequel(self, self));

        Assert.Equal(1, calculator.GetGroupAnimeId(1));
    }

    [Fact]
    public void GetGroupAnimeId_IsStableAcrossRepeatedCalls()
    {
        var calculator = Calc(Sequel(new Anime(7, AirDate: Year(2000)), new Anime(3, AirDate: Year(2001))));

        var first = calculator.GetGroupAnimeId(3);

        // The second call is served from the memoised map rather than a rebuilt graph.
        Assert.Equal(first, calculator.GetGroupAnimeId(3));
        Assert.Equal(first, calculator.GetGroupAnimeId(7));
    }

    #endregion

    #region MinAirDate selection strategy

    [Fact]
    public void MinAirDate_SelectsTheEarliestAiringAnime()
    {
        var calculator = Calc(
            Sequel(new Anime(9, AirDate: Year(1998)), new Anime(2, AirDate: Year(2005))),
            strategy: MainAnimeSelectionStrategy.MinAirDate);

        // Chosen on air date, not on the lower ID.
        Assert.Equal(9, calculator.GetGroupAnimeId(2));
    }

    [Fact]
    public void MinAirDate_TreatsAMissingAirDateAsLastOfAll()
    {
        var calculator = Calc(
            Sequel(new Anime(1, AirDate: null), new Anime(2, AirDate: Year(2005))),
            strategy: MainAnimeSelectionStrategy.MinAirDate);

        Assert.Equal(2, calculator.GetGroupAnimeId(1));
    }

    #endregion

    #region Weighted selection strategy

    [Fact]
    public void Weighted_PrefersTheAnimeWithASequel()
    {
        var calculator = Calc(
            Sequel(new Anime(5), new Anime(1)),
            strategy: MainAnimeSelectionStrategy.Weighted);

        // Both are TV (3 points); anime 5 additionally scores for having a sequel (+2), which beats
        // the lowest-ID tiebreak that would otherwise pick anime 1.
        Assert.Equal(5, calculator.GetGroupAnimeId(1));
    }

    [Theory]
    [InlineData(AnimeType.OVA)]
    [InlineData(AnimeType.Web)]
    public void Weighted_PrefersTvOverTheLesserType(AnimeType lesserType)
    {
        var calculator = Calc(
            SameSetting(new Anime(9, Type: AnimeType.TV), new Anime(1, Type: lesserType)),
            strategy: MainAnimeSelectionStrategy.Weighted);

        // Both sides score one alternative version, so only the series type separates them.
        Assert.Equal(9, calculator.GetGroupAnimeId(1));
    }

    [Fact]
    public void Weighted_BreaksScoreTiesByLowestAnimeId()
    {
        var calculator = Calc(
            SameSetting(new Anime(9, Type: AnimeType.TV), new Anime(4, Type: AnimeType.TV)),
            strategy: MainAnimeSelectionStrategy.Weighted);

        Assert.Equal(4, calculator.GetGroupAnimeId(9));
    }

    #endregion

    #region Exclusions

    [Theory]
    [InlineData(AutoGroupExclude.None, true)]
    [InlineData(AutoGroupExclude.SameSetting, false)]
    public void Exclusions_SkipRelationsOfTheExcludedTypeOnly(AutoGroupExclude exclusions, bool expectedGrouped)
    {
        var calculator = Calc(SameSetting(new Anime(1), new Anime(2)), exclusions);

        if (expectedGrouped)
            AssertGrouped(calculator, 1, 2);
        else
            AssertNotGrouped(calculator, 1, 2);
    }

    [Fact]
    public void Exclusions_OnlyApplyToTheExcludedRelationType()
    {
        // Excluding SameSetting must not disturb a prequel/sequel pair.
        var calculator = Calc(
            Sequel(new Anime(1, AirDate: Year(2000)), new Anime(2, AirDate: Year(2001))),
            AutoGroupExclude.SameSetting);

        AssertGrouped(calculator, 1, 2);
    }

    [Theory]
    [InlineData(AutoGroupExclude.Movie, AnimeType.Movie)]
    [InlineData(AutoGroupExclude.Ova, AnimeType.OVA)]
    public void Exclusions_SkipRelationsInvolvingTheExcludedAnimeType(AutoGroupExclude exclusions, AnimeType relatedType)
    {
        var calculator = Calc(
            Sequel(new Anime(1, Type: AnimeType.TV, AirDate: Year(2000)), new Anime(2, Type: relatedType, AirDate: Year(2001))),
            exclusions);

        AssertNotGrouped(calculator, 1, 2);
    }

    [Fact]
    public void Exclusions_Movie_LeavesNonMovieRelationsAlone()
    {
        var calculator = Calc(
            Sequel(new Anime(1, Type: AnimeType.TV, AirDate: Year(2000)), new Anime(2, Type: AnimeType.TV, AirDate: Year(2001))),
            AutoGroupExclude.Movie);

        AssertGrouped(calculator, 1, 2);
    }

    #endregion

    #region Fuzzy title matching

    /// <summary>
    /// Builds a calculator whose only relation is a <see cref="RelationType.SameSetting"/> pair with
    /// the given titles, with fuzzy title testing switched on for the secondary relation types.
    /// </summary>
    private static AutoAnimeGroupCalculator FuzzyCalc(string firstTitle, string secondTitle)
        => Calc(
            SameSetting(new Anime(1, firstTitle), new Anime(2, secondTitle)),
            AutoGroupExclude.None,
            RelationType.SecondaryRelations);

    [Theory]
    [InlineData("Fullmetal Alchemist", "Fullmetal Alchemist Brotherhood", true)]
    [InlineData("Naruto", "Bleach", false)]
    // Were "The Movie" not stripped, the shared words would group two unrelated franchises.
    [InlineData("Bleach The Movie", "Naruto The Movie", false)]
    [InlineData("Bleach The Animation", "Naruto The Animation", false)]
    [InlineData("Gekijouban Naruto", "Naruto", true)]
    [InlineData("Gundam 00", "Gundam 2", true)]
    [InlineData("Cowboy-Bebop", "Cowboy Bebop", true)]
    // "Cowboy/Bebop" collapses to the single token "CowboyBebop", which matches neither word.
    [InlineData("Cowboy/Bebop", "Cowboy Bebop", false)]
    // Two of six tokens match, which fails the "half the words" rule (2 < 3), but the matched
    // characters (19) clear 40% of the shorter title (36 chars -> 14).
    [InlineData("Alphabetical Betamax Gamma Delta Epsilon Zeta", "Alphabetical Betamax Eta Theta Iota Kappa", true)]
    // Same two-of-six token overlap, but the matched characters (9) fall short of 40% of the
    // shorter title (26 chars -> 10).
    [InlineData("Alpha Beta Gamma Delta Epsilon Zeta", "Alpha Beta Eta Theta Iota Kappa", false)]
    public void FuzzyTitle_GroupsAnimeOnlyWhenTheTitlesOverlap(string firstTitle, string secondTitle, bool expectedGrouped)
    {
        var calculator = FuzzyCalc(firstTitle, secondTitle);

        if (expectedGrouped)
            AssertGrouped(calculator, 1, 2);
        else
            AssertNotGrouped(calculator, 1, 2);
    }

    [Fact]
    public void FuzzyTitle_IsNotAppliedToPrimaryRelationTypes()
    {
        // Prequel/Sequel is outside SecondaryRelations, so wildly different titles still group.
        var calculator = Calc(
            Sequel(new Anime(1, "Naruto", AirDate: Year(2000)), new Anime(2, "Bleach", AirDate: Year(2001))),
            AutoGroupExclude.None,
            RelationType.SecondaryRelations);

        AssertGrouped(calculator, 1, 2);
    }

    #endregion
}
