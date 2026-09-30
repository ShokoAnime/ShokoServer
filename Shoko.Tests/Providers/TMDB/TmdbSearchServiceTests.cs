using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Utilities;
using TMDbLib.Objects.Search;
using Xunit;

using ResourceLinkType = Shoko.Server.Providers.AniDB.ResourceLinkType;

// ReSharper disable StringLiteralTypo

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// Tests for <see cref="FuzzySearchService.FuzzyScoreAnyName"/> and
/// <see cref="SeriesSearch.NormalizeForIndex"/> behaviour relevant to TMDB auto-match scoring
/// for both shows and movies.
///</summary>
public class TmdbSearchServiceTests
{
    private static readonly FuzzySearchService Service = new();

    // ── FuzzyScoreAnyName: isNotExact gate ─────────────────────────────────
    // The scoring pass uses FuzzyScoreAnyName and only accepts results where
    // isNotExact==true (genuine edit-distance hit). Substring hits (isNotExact==false)
    // are excluded so spinoffs don't score TitleKindaMatches via prefix containment.

    [Fact]
    public void FuzzyScoreAnyName_SubstringHit_IsNotExactFalse()
    {
        // "Tensura" is a substring of "Tensura Nikki" — isNotExact should be false,
        // meaning the scoring path will NOT count this as fuzzyTitle=true.
        var names = new HashSet<string> { "Tensura Nikki: Tensei shitara Slime Datta Ken" };
        var score = Service.FuzzyScoreAnyName("Tensura", names);
        Assert.NotNull(score);
        Assert.False(score!.Value.isNotExact);
    }

    [Fact]
    public void FuzzyScoreAnyName_EditDistanceHit_IsNotExactTrue()
    {
        // "Kimetzu no Yaiba" is 1 edit from "Kimetsu no Yaiba" — isNotExact should be true,
        // meaning the scoring path WILL count this as fuzzyTitle=true.
        var names = new HashSet<string> { "Kimetsu no Yaiba" };
        var score = Service.FuzzyScoreAnyName("Kimetzu no Yaiba", names);
        Assert.NotNull(score);
        Assert.True(score!.Value.isNotExact);
    }

    [Fact]
    public void FuzzyScoreAnyName_OnePunchManSequel_IsNotExactFalse()
    {
        var names = new HashSet<string> { "One Punch Man 2nd Season" };
        var score = Service.FuzzyScoreAnyName("One Punch Man", names);
        Assert.NotNull(score);
        Assert.False(score!.Value.isNotExact);
    }

    // ── FuzzyScoreAnyName: Blood+ edge case ────────────────────────────────

    [Fact]
    public void FuzzyScoreAnyName_BloodPlus_SubstringHitAgainstBloodC()
    {
        // MinEditDistInText is a substring distance: "blood+" matches the prefix "blood " of
        // "blood c" at 1 edit (+ → space), within budget (GetMaxErrors(6) = 1).
        // Blood-C therefore scores TitleKindaMatches when the query is "Blood+".
        // This is acceptable because the correct Blood+ TMDB entry scores TitleMatches
        // via exact match — SortPriority(TitleMatches)=3 beats TitleKindaMatches=5.
        var names = new HashSet<string> { "Blood-C" };
        var score = Service.FuzzyScoreAnyName("Blood+", names);
        Assert.NotNull(score);
        Assert.True(score!.Value.isNotExact); // edit-distance hit, not substring
    }

    // ── FuzzyScoreAnyName: Re:Zero abbreviated title ────────────────────────

    [Fact]
    public void FuzzyScoreAnyName_ReZero_ShortTitleSubstringHitOnly()
    {
        // "re zero" is a prefix of the long form → substring hit (isNotExact:false).
        // The edit-distance path does not fire; PrefixMatchesAnyName is what saves the match.
        var names = new HashSet<string> { "Re:Zero Starting Life in Another World" };
        var score = Service.FuzzyScoreAnyName("Re:Zero", names);
        Assert.NotNull(score);
        Assert.False(score!.Value.isNotExact);
    }

    // ── FuzzyScoreAnyName: single-character query gap (known limitation) ────

    [Fact]
    public void FuzzyScoreAnyName_SingleCharTitle_OnlySubstringHit()
    {
        // "C" is a substring of the TMDB title → isNotExact:false.
        // The scoring path excludes these (it gates on isNotExact:true),
        // so the match does not contribute to fuzzyTitle.
        var names = new HashSet<string> { "[C] - The Money of Soul and Possibility Control" };
        var score = Service.FuzzyScoreAnyName("C", names);
        Assert.NotNull(score);
        Assert.False(score!.Value.isNotExact);
    }

    // ── NormalizeForIndex: wave dash U+301C ─────────────────────────────────

    [Theory]
    [InlineData("〜", "")]                                        // solo wave dash → space, collapses to empty after trim
    [InlineData("Monogatari〜Series", "monogatari series")]       // wave dash as separator
    [InlineData("〜Series〜", "series")]                          // leading/trailing wave dash stripped by whitespace collapse
    public void NormalizeForIndex_WaveDash_MapsToSpace(string input, string expected)
        => Assert.Equal(expected, SeriesSearch.NormalizeForIndex(input).Trim());

    // ── NormalizeForIndex: fullwidth tilde U+FF5E ────────────────────────────

    [Fact]
    public void NormalizeForIndex_FullwidthTilde_MappedToSpace()
    {
        // U+FF5E ～ is decomposed by NFKD to ASCII tilde ~ before the separator mapping runs,
        // so it now gets the same space treatment as ASCII '~' and U+301C 〜.
        var result = SeriesSearch.NormalizeForIndex("Foo～Bar");
        Assert.Equal("foo bar", result);
    }

    // ── NormalizeForIndex: superscript decomposition ─────────────────────────

    [Fact]
    public void NormalizeForIndex_SuperscriptThree_DecomposesToDigit()
    {
        // U+00B3 ³ decomposes to '3' under NFKD — "C³" → "c3".
        Assert.Equal("c3", SeriesSearch.NormalizeForIndex("C³"));
    }

    // ── NormalizeForIndex: exclamation-mark collision (known limitation) ─────
    // '!' is in the punctuation-to-space map, so K-On! and K-On!! both normalize
    // to "k on". Disambiguation must fall back to air date and episode count.

    [Fact]
    public void NormalizeForIndex_KOn_SingleAndDoubleExclamationCollide()
    {
        Assert.Equal(SeriesSearch.NormalizeForIndex("K-On!"), SeriesSearch.NormalizeForIndex("K-On!!"));
    }

    [Fact]
    public void NormalizeForIndex_Working_AllExclamationVariantsCollide()
    {
        var s2 = SeriesSearch.NormalizeForIndex("Working!!");
        var s3 = SeriesSearch.NormalizeForIndex("Working!!!");
        Assert.Equal(s2, s3);
    }

    // ── NormalizeForIndex: subtitle-stripped colon split ─────────────────────
    // The show scorer strips the subtitle at the first colon for non-Japanese titles
    // (e.g. "Fairy Tail: 100 Years Quest" → "Fairy Tail"). This stripped form is
    // fuzzy-eligible only — it cannot produce ExactMatch — so a parent show that
    // matches via its short name scores TitleKindaMatches at most, while a specific
    // TMDB entry whose title exact-matches the full title scores TitleMatches and wins.
    // The prequel-traversal layer also tries the current anime's own main title when
    // the root-series search is tried, and prefers whichever scores better.

    [Fact]
    public void NormalizeForIndex_SubtitleStrippedAtColon_ParentTitleIsolated()
    {
        // "Fairy Tail: 100 Years Quest" strips to "Fairy Tail" at the colon.
        // The stripped form normalizes the same as the parent show title, confirming
        // PrefixMatchesAnyName would fire — but since it's fuzzy-only it cannot
        // produce ExactMatch and the parent scores at most TitleKindaMatches.
        const string fullTitle = "Fairy Tail: 100 Years Quest";
        var colonIndex = fullTitle.IndexOf(':');
        var strippedFromFull = SeriesSearch.NormalizeForIndex(fullTitle[..colonIndex].TrimEnd());
        var parentTitle = SeriesSearch.NormalizeForIndex("Fairy Tail");
        Assert.Equal(parentTitle, strippedFromFull);

        // The full title normalizes differently, so the specific TMDB entry that
        // exact-matches the full title can score TitleMatches and win.
        var normalizedFullTitle = SeriesSearch.NormalizeForIndex("Fairy Tail: 100 Years Quest");
        Assert.NotEqual(strippedFromFull, normalizedFullTitle);
    }

    // ── IsShortFormByEpisodeCount: OVA/Web movie routing threshold ───────────
    // OVA and Web anime with ≤4 main episodes route to movie search first,
    // because TMDB may model them as standalone films even though AniDB treats
    // them as a series (e.g. "Grudge of Edinburgh" = 2 AniDB episodes → 2 TMDB movies).

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]   // two-part film like Grudge of Edinburgh
    [InlineData(4, true)]   // upper bound of short-form heuristic
    [InlineData(5, false)]  // five-episode OVA stays on show search path
    [InlineData(13, false)]
    public void IsShortFormByEpisodeCount_Threshold(int count, bool expected)
        => Assert.Equal(expected, TmdbSearchService.IsShortFormByEpisodeCount(count));

    // ── FullMovieTitle ──────────────────────────────────────────────────────
    // An anime without a title in a language never has its film searched by the episode's title alone (" Episode 1").

    [Theory]
    [InlineData(null, "Episode 1", false, null)]
    [InlineData("", "Episode 1", false, null)]
    [InlineData(" ", "The Beginning", false, null)]
    [InlineData("Kaichuu!", null, false, null)]
    [InlineData("Kaichuu!", "The Beginning", false, "Kaichuu! The Beginning")]
    [InlineData("Kaichuu!", "Movie 3", true, "Kaichuu! 3")]
    public void FullMovieTitle_NeedsBothParts(string? animeTitle, string? subTitle, bool isGeneric, string? expected)
        => Assert.Equal(expected, TmdbSearchService.FullMovieTitle(animeTitle, subTitle, 3, isGeneric));

    // ── OwnTitleWins ────────────────────────────────────────────────────────
    // The anime's own show beats its prequel's when rated higher, or when it also agrees on the date.

    private static readonly DateOnly _airedOn = new(2008, 4, 2);

    [Theory]
    [InlineData(MatchRating.DateAndTitleMatches, MatchRating.TitleMatches, true)]
    [InlineData(MatchRating.DateAndTitleKindaMatches, MatchRating.TitleMatches, true)]
    [InlineData(MatchRating.DateAndTitleKindaMatches, MatchRating.TitleKindaMatches, true)]
    [InlineData(MatchRating.DateMatches, MatchRating.TitleMatches, false)]
    [InlineData(MatchRating.TitleMatches, MatchRating.TitleMatches, false)]
    [InlineData(MatchRating.DateAndTitleKindaMatches, MatchRating.DateAndTitleMatches, false)]
    [InlineData(MatchRating.TitleKindaMatches, MatchRating.DateMatches, false)]
    public void OwnTitleWins_NeedsADateOverATitleAlone(MatchRating own, MatchRating prequels, bool expected)
        => Assert.Equal(expected, TmdbSearchService.OwnTitleWins(new(own, null, null), new(prequels, null, null), _airedOn, 12));

    // A sequel's own show, begun with it, beats a prequel's show begun years before that only
    // shares the prequel's title.

    [Theory]
    [InlineData(0, 2922, true)]
    [InlineData(3, 2922, true)]
    [InlineData(10, 2922, false)]
    [InlineData(0, 200, false)]
    public void OwnTitleWins_TakesAShowBegunWithTheAnimeOnItsDateAlone(int ownDaysApart, int prequelDaysBefore, bool expected)
        => Assert.Equal(
            expected,
            TmdbSearchService.OwnTitleWins(
                new(MatchRating.DateMatches, _airedOn.AddDays(ownDaysApart), 155),
                new(MatchRating.TitleMatches, _airedOn.AddDays(-prequelDaysBefore), 228),
                _airedOn,
                154
            )
        );

    // A show whose title only loosely agrees cannot hold an anime with more
    // than three times its episodes, however well the years agree.

    [Theory]
    [InlineData(9, false)]
    [InlineData(17, true)]
    [InlineData(null, true)]
    public void OwnTitleWins_NeedsEnoughEpisodesForALooseTitle(int? ownEpisodes, bool expected)
        => Assert.Equal(
            expected,
            TmdbSearchService.OwnTitleWins(
                new(MatchRating.DateAndTitleKindaMatches, _airedOn.AddDays(-83), ownEpisodes),
                new(MatchRating.TitleMatches, _airedOn.AddDays(-1634), 100),
                _airedOn,
                50
            )
        );

    // ── AniDB hints ─────────────────────────────────────────────────────────
    // A hint wins a tie, and is taken alone only when the search took nothing and it is of a kind searched for.

    private static AniDB_Resource Resource(ResourceLinkType type, params string[] identifiers)
        => new() { AnimeID = 1, ResourceType = type, Identifiers = [.. identifiers] };

    [Fact]
    public void TmdbHintsOf_ReadsTheIDAndKind_AndSkipsWhatItCannotRead()
    {
        var hints = TmdbSearchService.TmdbHintsOf(
        [
            Resource(ResourceLinkType.TMDB, "26209", "tv"),
            Resource(ResourceLinkType.TMDB, "357405", "movie"),
            Resource(ResourceLinkType.TMDB, "26209", "tv"),
            Resource(ResourceLinkType.TMDB, "12", "collection"),
            Resource(ResourceLinkType.TMDB, "abc", "tv"),
            Resource(ResourceLinkType.TMDB, "0", "tv"),
            Resource(ResourceLinkType.TMDB, "44"),
            Resource(ResourceLinkType.IMDb, "tt0409591"),
        ]);

        Assert.Equal([(26209, false), (357405, true)], hints.Select(hint => (hint.TmdbID, hint.IsMovie)));
        Assert.Equal((MetadataSource.TMDB, MetadataEntityType.Series, "26209"), (hints[0].ID.Source, hints[0].ID.EntityType, hints[0].ID.ID));
        Assert.Equal(MetadataEntityType.Movie, hints[1].ID.EntityType);
    }

    // Read from the anime's links on other sources, once each with what named them; an
    // episode whose show is not known is no hint.
    [Fact]
    public void TmdbHintsOf_ReadsTheCrossSourceHints()
    {
        var namedBy = MetadataGuid.Parse("anilist://series/10");
        var hints = TmdbSearchService.TmdbHintsOf(
        [
            new MetadataAutoLinkHint { ID = MetadataGuid.Parse("tmdb://series/5"), AnidbAnimeID = 1, NamedBy = [namedBy] },
            new MetadataAutoLinkHint { ID = MetadataGuid.Parse("tmdb://movie/7"), AnidbAnimeID = 1, AnidbEpisodeID = 3, NamedBy = [namedBy, MetadataGuid.Parse("anilist://movie/2")] },
            new MetadataAutoLinkHint { ID = MetadataGuid.Parse("tmdb://episode/9"), AnidbAnimeID = 1, NamedBy = [namedBy] },
            new MetadataAutoLinkHint { ID = MetadataGuid.Parse("tmdb://series/5"), AnidbAnimeID = 1, NamedBy = [MetadataGuid.Parse("anilist://series/11")] },
        ]);

        Assert.Equal([(5, false, (int?)null), (7, true, 3)], hints.Select(hint => (hint.TmdbID, hint.IsMovie, hint.AnidbEpisodeID)));
        Assert.All(hints, hint => Assert.Equal(MetadataAutoLinkOrigin.CrossSourceLink, hint.Origin));
        Assert.All(hints, hint => Assert.Contains("anilist://series/10", hint.Source));
    }

    [Fact]
    public void ImdbIDsOf_TakesTitleIDsOnly()
        => Assert.Equal(
            ["tt0409591"],
            TmdbSearchService.ImdbIDsOf(
            [
                Resource(ResourceLinkType.IMDb, "tt0409591"),
                Resource(ResourceLinkType.IMDb, "tt0409591"),
                Resource(ResourceLinkType.IMDb, "nm0000001"),
                Resource(ResourceLinkType.IMDb),
                Resource(ResourceLinkType.TMDB, "26209", "tv"),
            ])
        );

    [Theory]
    // A short OVA or web release is looked for as a film too.
    [InlineData(AnimeType.OVA, true, true, true)]
    [InlineData(AnimeType.OVA, false, true, false)]
    [InlineData(AnimeType.Web, true, true, true)]
    public void HintKinds_FollowWhatTheSearchLooksFor(AnimeType type, bool isShortForm, bool shows, bool movies)
        => Assert.Equal((shows, movies), TmdbSearchService.HintKinds(type, isShortForm));

    private static readonly TmdbSearchService.TmdbHint _showHint = new(26209, false);

    // Whether the search took something is the core's to judge, so a hint
    // the engine turned down may still be taken.
    [Fact]
    public void HintRejection_LeavesAHintOfTheKindSearchedFor()
        => Assert.Null(TmdbSearchService.HintRejection(_showHint, AnimeType.TVSeries, true, MatchRating.None, MatchRejectionReason.TitleMismatch, "Neither matched.", false));

    // An anime of the other and unknown types is not searched, so its hints
    // are only listed.
    [Theory]
    [InlineData(AnimeType.Other)]
    [InlineData(AnimeType.Unknown)]
    public void HintRejection_ListsTheHintsOfAnAnimeNotSearched(AnimeType type)
    {
        var (shows, _) = TmdbSearchService.HintKinds(type, isShortForm: true);
        var rejection = TmdbSearchService.HintRejection(_showHint, type, shows, MatchRating.TitleMatches, MatchRejectionReason.None, null, false);

        Assert.Equal(MatchRejectionReason.TypeMismatch, rejection?.Reason);
    }

    // The hints that may be taken lead, a film before a show for a
    // short-form anime, then the best rated, then AniDB's order.
    [Fact]
    public void HintsInOrder_PutsTheOneToTakeFirst()
    {
        var anime = new AniDB_Anime { AnimeID = 30 };
        var episode = new AniDB_Episode { EpisodeID = 300, AnimeID = 30 };
        TmdbAutoSearchResult Show(int id, MatchRating rating, bool rejected = false)
            => new(anime, new SearchTv { Id = id, Name = $"Show {id}" }, rating)
            {
                Origin = MetadataAutoLinkOrigin.AnidbResource,
                Rejection = rejected ? new() { Reason = MatchRejectionReason.Restricted } : null,
            };
        TmdbAutoSearchResult Film(int id, MatchRating rating)
            => new(anime, episode, new SearchMovie { Id = id, Title = $"Film {id}" }, rating) { Origin = MetadataAutoLinkOrigin.AnidbResource };
        IReadOnlyList<TmdbAutoSearchResult> hints =
        [
            Show(1, MatchRating.UserVerified, rejected: true),
            Show(2, MatchRating.TitleKindaMatches),
            Show(3, MatchRating.DateAndTitleMatches),
            Film(4, MatchRating.FirstAvailable),
            Show(5, MatchRating.DateAndTitleMatches),
        ];

        Assert.Equal([4, 3, 5, 2, 1], TmdbSearchService.HintsInOrder(hints, filmsFirst: true).Select(ID));
        Assert.Equal([3, 5, 2, 4, 1], TmdbSearchService.HintsInOrder(hints, filmsFirst: false).Select(ID));

        static int ID(TmdbAutoSearchResult result) => result.IsMovie ? result.TmdbMovie.ID : result.TmdbShow.ID;
    }

    // A hint the other links name, rated higher, keeps its lead over one the
    // AniDB resources name, and a tie goes to the AniDB resources' one.
    [Fact]
    public void BestFirst_KeepsTheHintsOfBothOriginsInTheirOrder()
    {
        var anime = new AniDB_Anime { AnimeID = 30 };
        TmdbAutoSearchResult Show(int id, MatchRating rating, MetadataAutoLinkOrigin origin)
            => new(anime, new SearchTv { Id = id, Name = $"Show {id}" }, rating) { Origin = origin };
        var hints = TmdbSearchService.HintsInOrder(
            [
                Show(1, MatchRating.FirstAvailable, MetadataAutoLinkOrigin.AnidbResource),
                Show(2, MatchRating.TitleMatches, MetadataAutoLinkOrigin.AnidbResource),
                Show(3, MatchRating.DateAndTitleMatches, MetadataAutoLinkOrigin.CrossSourceLink),
                Show(4, MatchRating.TitleMatches, MetadataAutoLinkOrigin.CrossSourceLink),
            ],
            filmsFirst: false
        );

        var ordered = TmdbSearchService.BestFirst([Show(5, MatchRating.TitleKindaMatches, MetadataAutoLinkOrigin.Search), .. hints]);

        Assert.Equal([5, 3, 2, 4, 1], ordered.Select(result => result.TmdbShow!.ID));
    }

    [Fact]
    public void TheAutoSearchPutsWhatItTookFirstAndKeepsEachCandidateOnce()
    {
        var anime = new AniDB_Anime { AnimeID = 30 };
        var first = new AniDB_Episode { EpisodeID = 300, AnimeID = 30 };
        var second = new AniDB_Episode { EpisodeID = 301, AnimeID = 30 };
        MetadataAutoLinkRejection Lost(MatchRejectionReason reason) => new() { Reason = reason };
        TmdbAutoSearchResult Movie(AniDB_Episode episode, int id, MetadataAutoLinkRejection? rejection = null)
            => new(anime, episode, new SearchMovie { Id = id, Title = $"Film {id}" }) { IsRemote = true, Rejection = rejection };

        var ordered = TmdbSearchService.BestFirst([
            // Turned down for one title, then taken for the next.
            Movie(first, 600, Lost(MatchRejectionReason.TitleMismatch)),
            Movie(first, 601, Lost(MatchRejectionReason.DateMismatch)),
            Movie(first, 600),
            // The same film for another episode is another candidate.
            Movie(second, 600, Lost(MatchRejectionReason.Outranked)),
            Movie(second, 602),
            new(anime, new SearchTv { Id = 5, Name = "Show" }) { Rejection = Lost(MatchRejectionReason.TypeMismatch) },
        ]);

        Assert.Equal(
            [(300, 600, false), (301, 602, false), (300, 601, true), (301, 600, true), (0, 5, true)],
            ordered.Select(result => (result.AnidbEpisode?.EpisodeID ?? 0, result.IsMovie ? result.TmdbMovie.ID : result.TmdbShow.ID, result.Rejection is not null))
        );
    }

    // The same show found by the search and linked to the prequel is listed
    // once in each group, the search's first.
    [Fact]
    public void TheAutoSearchKeepsAShowOnceInEachGroup()
    {
        var anime = new AniDB_Anime { AnimeID = 15067 };
        var prequelLink = TmdbSearchService.PrequelLink(anime, new AniDB_Anime { AnimeID = 1 }, MatchRating.UserVerified, new TMDB_Show(60572) { EnglishTitle = "Pokemon" });

        var ordered = TmdbSearchService.BestFirst([
            prequelLink,
            new(anime, new SearchTv { Id = 220150, Name = "Horizons" }, MatchRating.TitleMatches) { IsRemote = true, Rejection = new() { Reason = MatchRejectionReason.DateMismatch } },
            new(anime, new SearchTv { Id = 60572, Name = "Pokemon" }, MatchRating.TitleMatches) { IsRemote = true },
        ]);

        Assert.Equal(
            [(MetadataAutoLinkOrigin.Search, 60572, MatchRating.TitleMatches), (MetadataAutoLinkOrigin.Search, 220150, MatchRating.TitleMatches), (MetadataAutoLinkOrigin.PrequelLink, 60572, MatchRating.None)],
            ordered.Select(result => (result.Origin, result.TmdbShow!.ID, result.MatchRating))
        );
    }

    [Fact]
    public void HintRejection_ListsAHintOfTheOtherKind_NeverTakingIt()
    {
        var film = new TmdbSearchService.TmdbHint(357405, true);
        var rejection = TmdbSearchService.HintRejection(film, AnimeType.TVSeries, false, MatchRating.TitleMatches, MatchRejectionReason.None, null, false);

        Assert.Equal(MatchRejectionReason.TypeMismatch, rejection?.Reason);
        Assert.Contains("357405", rejection?.Details);
    }

    [Fact]
    public void HintRejection_KeepsTheEnginesFilter_AndRefusesAnUnplacedFilm()
    {
        var film = new TmdbSearchService.TmdbHint(357405, true, "tt0409591");

        Assert.Equal(
            MatchRejectionReason.Restricted,
            TmdbSearchService.HintRejection(film, AnimeType.Movie, true, MatchRating.TitleMatches, MatchRejectionReason.Restricted, "Adult.", false)?.Reason
        );
        var unplaced = TmdbSearchService.HintRejection(film, AnimeType.Movie, true, MatchRating.TitleMatches, MatchRejectionReason.None, null, true);
        Assert.Equal(MatchRejectionReason.Other, unplaced?.Reason);
        Assert.Contains("tt0409591", unplaced?.Details);
    }

    // A Kite (AniDB 314): the film came out the day the first of its two OVA
    // episodes did, and the engine, reading dates by year, rates both alike.
    [Fact]
    public void PlaceFilm_TheOnlyTiedEpisodeAiredWithTheFilmTakesIt()
    {
        var placed = TmdbSearchService.PlaceFilm(
            [
                (MatchRating.DateAndTitleMatches, [new DateOnly(1998, 2, 25)]),
                (MatchRating.DateAndTitleMatches, [new DateOnly(1998, 10, 25)]),
            ],
            [new DateOnly(1998, 2, 25)]
        );

        Assert.Equal((0, false), placed);
    }

    // Dirty Pair (AniDB 1174): three episodes out on the same day as the
    // film, so the date tells none of them apart.
    [Fact]
    public void PlaceFilm_SeveralTiedEpisodesAiredWithTheFilmLeaveItUnplaced()
    {
        var day = new DateOnly(1990, 1, 25);
        var (_, unplaced) = TmdbSearchService.PlaceFilm(
            [
                (MatchRating.DateAndTitleMatches, [day]),
                (MatchRating.DateAndTitleMatches, [day]),
                (MatchRating.DateAndTitleMatches, [day]),
            ],
            [day]
        );

        Assert.True(unplaced);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void PlaceFilm_AnEpisodeMayBeThreeDaysFromAnyRelease(int daysApart, bool unplaced)
    {
        var placed = TmdbSearchService.PlaceFilm(
            [
                (MatchRating.DateMatches, [new DateOnly(2001, 1, 1)]),
                (MatchRating.DateMatches, [new DateOnly(2001, 6, 1)]),
            ],
            // The theatrical release is far from both; a later one is near the second.
            [new DateOnly(2001, 3, 1), new DateOnly(2001, 6, 1).AddDays(daysApart)]
        );

        Assert.Equal((unplaced ? 0 : 1, unplaced), placed);
    }

    [Fact]
    public void PlaceFilm_TheBestRatedEpisodeNeedsNoDate()
    {
        var placed = TmdbSearchService.PlaceFilm(
            [
                (MatchRating.DateMatches, [new DateOnly(1998, 2, 25)]),
                (MatchRating.DateAndTitleMatches, []),
            ],
            [new DateOnly(1998, 2, 25)]
        );

        Assert.Equal((1, false), placed);
    }

    [Fact]
    public void PlaceFilm_NothingMatchingLeavesItUnplacedWhateverTheDate()
    {
        var day = new DateOnly(1998, 2, 25);

        Assert.Equal((0, true), TmdbSearchService.PlaceFilm([(MatchRating.None, [day]), (MatchRating.None, [day.AddYears(1)])], [day]));
        Assert.Equal((0, false), TmdbSearchService.PlaceFilm([(MatchRating.None, [day])], [day]));
    }

    // An anime the search would not look for yet is not linked from a hint
    // either, the hint still being listed with why.
    [Fact]
    public void HintRejection_RefusesAHintForAnAnimeNotAiredYet()
    {
        var now = new DateTime(2026, 9, 28, 12, 0, 0);
        var window = TimeSpan.FromDays(15);
        var notAired = TmdbSearchService.NotAired(now.AddDays(40), false, now, window);
        var rejection = TmdbSearchService.HintRejection(_showHint, AnimeType.TVSeries, true, MatchRating.DateAndTitleMatches, MatchRejectionReason.None, null, false, notAired);

        Assert.Equal(MatchRejectionReason.Other, rejection?.Reason);
        Assert.Contains("26209", rejection?.Details);
        // Without an air date, the anime or the film's episode is not aired either.
        Assert.NotNull(TmdbSearchService.NotAired(null, false, now, window));
        Assert.NotNull(TmdbSearchService.NotAired(null, true, now, window));
    }

    // The same window as the search's: aired, or airing within it.
    [Theory]
    [InlineData(-400, true)]
    [InlineData(0, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public void AiredWithin_FollowsTheSearchWindow(int days, bool searched)
    {
        var now = new DateTime(2026, 9, 28, 12, 0, 0);

        Assert.Equal(searched, TmdbSearchService.AiredWithin(now.AddDays(days), now, TimeSpan.FromDays(15)));
        Assert.Equal(searched, TmdbSearchService.NotAired(now.AddDays(days), false, now, TimeSpan.FromDays(15)) is null);
        Assert.False(TmdbSearchService.AiredWithin(null, now, TimeSpan.FromDays(15)));
    }

    [Fact]
    public void HintedFirst_MovesTheHintedCandidatesUp_KeepingTheOrder()
        => Assert.Equal(
            [3, 1, 2, 4],
            TmdbSearchService.HintedFirst([1, 2, 3, 4], [new(3, false), new(4, true)], isMovie: false)
        );

    // Rated alike, the show the anime's AniDB resources name wins, whichever
    // titles found it.
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void OwnTitleWins_AHintSettlesATie(bool ownHinted, bool prequelsHinted, bool expected)
        => Assert.Equal(
            expected,
            TmdbSearchService.OwnTitleWins(
                new(MatchRating.TitleMatches, null, null, ownHinted),
                new(MatchRating.TitleMatches, null, null, prequelsHinted),
                _airedOn,
                12
            )
        );

    // ── NormalizeForIndex: exclamation collision in movie context ─────────────
    // Movies lack an episode-count tiebreaker, so when two candidates collide on
    // normalized title the scorer falls back to the first result in rating order.
    // The correct movie should score DateAndTitleMatches over the OVA/extra
    // carrying the same base title, but this test documents the collision so any
    // future change to punctuation handling is intentional.

    [Fact]
    public void NormalizeForIndex_MovieExclamationCollision_TitlesTieOnNormalized()
    {
        // e.g. "Precure All Stars Movie: Haru no Carnival♪" and a variant with
        // different punctuation would both normalize the same way.
        // More concretely: a main movie and a bonus short sharing a base title
        // that differs only in trailing punctuation score identically — the date
        // match is the lever to break the tie.
        var mainMovie = SeriesSearch.NormalizeForIndex("Fairy Tail: Phoenix Priestess");
        var bonusShort = SeriesSearch.NormalizeForIndex("Fairy Tail: Phoenix Priestess!");
        Assert.Equal(mainMovie, bonusShort);
    }
}
