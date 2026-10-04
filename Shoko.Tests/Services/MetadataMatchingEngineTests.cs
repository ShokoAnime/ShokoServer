using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Filters;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Covers <see cref="MetadataMatchingEngine"/>: lining a source's episodes up against
///   AniDB's, and rating the series and films a search offers for an anime.
/// </summary>
/// <remarks>
///   Everything here runs on stubs: the matcher reads no repository and touches no database.
/// </remarks>
public class MetadataMatchingEngineTests
{
    private static readonly DateOnly _firstAired = new(2024, 1, 7);

    private static MetadataMatchingEngine Matcher() => new(NullLogger<MetadataMatchingEngine>.Instance, new FuzzySearchService());

    #region Within seasons

    // The seasoned strategy's strongest tier: the air date and the title both
    // agree, which is the only rating its first pass accepts.
    [Fact]
    public void DateAndTitleWithinSeasons_PairsOnDateAndTitle()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired, title: "Alpha"),
            AnidbEpisode(2, 2, _firstAired.AddDays(7), title: "Beta"),
            AnidbEpisode(3, 3, _firstAired.AddDays(14), title: "Gamma"),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired, "Alpha"),
            ProviderEpisode(102, 2, 1, _firstAired.AddDays(7), "Beta"),
            ProviderEpisode(103, 3, 1, _firstAired.AddDays(14), "Gamma"),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        Assert.Equal(3, matches.Count);
        Assert.All(matches, match => Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating));
        Assert.Equal(["101", "102", "103"], matches.Select(match => match.Candidate?.ID.ID));
    }

    // The source only has the regular broadcast date, not AniDB's early showing.
    [Fact]
    public void AnEpisodeShownEarly_IsMatchedOnItsRegularDate()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired.AddDays(-30), title: "Alpha", regularAirDate: _firstAired),
            AnidbEpisode(2, 2, _firstAired.AddDays(7), title: "Beta"),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired, "Alpha"),
            ProviderEpisode(102, 2, 1, _firstAired.AddDays(7), "Beta"),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        Assert.All(matches, match => Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating));
        Assert.Equal(["101", "102"], matches.Select(match => match.Candidate?.ID.ID));
    }

    // An episode shown early is out already, so a regular date still to come
    // does not hold it back as a future episode.
    [Fact]
    public void AnEpisodeShownEarly_IsNotSkippedAsFuture()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var anidb = new List<IAnidbEpisode> { AnidbEpisode(1, 1, today.AddDays(-3), regularAirDate: today.AddDays(10)) };
        var provider = new List<IEpisode> { ProviderEpisode(101, 1, null, today.AddDays(10)) };

        var options = new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateThenNumber };
        var match = Assert.Single(Matcher().MatchEpisodes(anidb, provider, options: options));

        Assert.Equal("101", match.Candidate?.ID.ID);
        Assert.Equal(MatchRating.DateAndNumberMatches, match.Rating);
    }

    // A title that is right but a date that is nowhere near still places the
    // episode, one tier down, since the title on its own is evidence here.
    [Fact]
    public void DateAndTitleWithinSeasons_FallsBackToTitle_WhenDatesDisagree()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired, title: "Alpha"),
            AnidbEpisode(2, 2, _firstAired.AddDays(7), title: "Beta"),
        };
        var provider = new List<IEpisode>
        {
            // Both dated years off, so only the titles can line these up.
            ProviderEpisode(101, 1, 1, _firstAired.AddYears(3), "Beta"),
            ProviderEpisode(102, 2, 1, _firstAired.AddYears(3).AddDays(7), "Alpha"),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        Assert.All(matches, match => Assert.Equal(MatchRating.TitleMatches, match.Rating));
        Assert.Equal("102", matches[0].Candidate?.ID.ID);
        Assert.Equal("101", matches[1].Candidate?.ID.ID);
    }

    // The special is an exact date-and-title match for a season 1 candidate,
    // so only the separate pools keep it from taking one.
    [Fact]
    public void DateAndTitleWithinSeasons_KeepsSpecialsOutOfTheOrdinaryPool()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired, title: "Alpha"),
            AnidbEpisode(3, 1, _firstAired.AddDays(7), EpisodeType.Special, "Beta"),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired, "Alpha"),
            // Season 1, and everything the special has to go on.
            ProviderEpisode(102, 2, 1, _firstAired.AddDays(7), "Beta"),
            ProviderEpisode(103, 1, 0, _firstAired.AddDays(3), "Bonus"),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        var special = Assert.Single(matches, match => match.AnidbEpisode.Type is EpisodeType.Special);
        Assert.NotEqual("102", special.Candidate?.ID.ID);
        var ordinary = Assert.Single(matches, match => match.AnidbEpisode.Type is EpisodeType.Episode);
        Assert.Equal("101", ordinary.Candidate?.ID.ID);
    }

    #endregion

    #region Date, then number

    [Fact]
    public void DateThenNumber_PairsOnDate_AndRaisesRatingWhenNumberAgrees()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired),
            AnidbEpisode(2, 2, _firstAired.AddDays(7)),
            AnidbEpisode(3, 3, _firstAired.AddDays(14)),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, null, _firstAired),
            ProviderEpisode(102, 2, null, _firstAired.AddDays(7)),
            ProviderEpisode(103, 3, null, _firstAired.AddDays(14)),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateThenNumber });

        Assert.All(matches, match => Assert.Equal(MatchRating.DateAndNumberMatches, match.Rating));
        Assert.Equal(["101", "102", "103"], matches.Select(match => match.Candidate?.ID.ID));
    }

    [Fact]
    public void DateThenNumber_SeparatesEpisodesSharingAnAirDate_ByNumber()
    {
        var sameDay = _firstAired;
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, sameDay),
            AnidbEpisode(2, 2, sameDay),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, null, sameDay),
            ProviderEpisode(102, 2, null, sameDay),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateThenNumber });

        Assert.Equal("101", matches[0].Candidate?.ID.ID);
        Assert.Equal("102", matches[1].Candidate?.ID.ID);
        Assert.All(matches, match => Assert.Equal(MatchRating.DateAndNumberMatches, match.Rating));
    }

    // An agreeing number raises a date match but never makes one: with no dates,
    // what comes back is the weak positional fallback.
    [Fact]
    public void DateThenNumber_NumberAloneNeverPlacesAnEpisode()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired),
            AnidbEpisode(2, 2, _firstAired.AddDays(7)),
            AnidbEpisode(3, 3, _firstAired.AddDays(14)),
        };
        // Numbered to agree exactly, but dated not at all.
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, null, null),
            ProviderEpisode(102, 2, null, null),
            ProviderEpisode(103, 3, null, null),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateThenNumber });

        Assert.DoesNotContain(matches, match => match.Rating is MatchRating.DateAndNumberMatches);
        Assert.All(matches, match => Assert.Equal(MatchRating.FirstAvailable, match.Rating));
    }

    // The rating, not the pairing, shows which axis placed each episode, since
    // reconciliation is free to reorder weak pairings without number evidence.
    [Fact]
    public void DateThenNumber_DoesNotTakeAnAgreeingNumber_AsCorroboration()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired),
            AnidbEpisode(2, 2, _firstAired.AddDays(7)),
        };
        // Numbered 1 and 2 as AniDB is, but the source dates them the other way
        // round, so the number can only ever agree by coincidence.
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, null, _firstAired.AddDays(7)),
            ProviderEpisode(102, 2, null, _firstAired),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateThenNumber });

        Assert.All(matches, match => Assert.Equal(MatchRating.DateMatches, match.Rating));
        Assert.DoesNotContain(matches, match => match.Rating is MatchRating.DateAndNumberMatches);
        // Both matches are weak and came out reversed, so reconciliation swaps
        // them back into AniDB order.
        Assert.Equal(["101", "102"], matches.Select(match => match.Candidate?.ID.ID));
    }

    // The one place a number does place an episode, and even here the offset it
    // sits on is derived purely from neighbours the dates placed.
    [Fact]
    public void DateThenNumber_PlacesUndatedEpisodes_OnTheOffsetTheDatedOnesAgreeOn()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired),
            AnidbEpisode(2, 2, _firstAired.AddDays(7)),
            AnidbEpisode(3, 3, _firstAired.AddDays(14)),
            AnidbEpisode(4, 4, _firstAired.AddDays(21)),
        };
        // Numbered ten out from AniDB, and episode 13 has no date of its own.
        var provider = new List<IEpisode>
        {
            ProviderEpisode(111, 11, null, _firstAired),
            ProviderEpisode(112, 12, null, _firstAired.AddDays(7)),
            ProviderEpisode(113, 13, null, null),
            ProviderEpisode(114, 14, null, _firstAired.AddDays(21)),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateThenNumber });

        Assert.Equal(["111", "112", "113", "114"], matches.Select(match => match.Candidate?.ID.ID));
        Assert.Equal(MatchRating.DateOffsetMatches, matches[2].Rating);
        Assert.All(matches.Where(match => match.AnidbEpisode.EpisodeNumber != 3), match => Assert.Equal(MatchRating.DateMatches, match.Rating));
    }

    #endregion

    #region Auto

    [Fact]
    public void Auto_PicksTheSeasonedPath_WhenTheSourceHasSeasonNumbers()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired),
            AnidbEpisode(2, 2, _firstAired.AddDays(7)),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired),
            ProviderEpisode(102, 2, 1, _firstAired.AddDays(7)),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider);

        // The seasoned path has no number tier; the flat path would have raised
        // these to DateAndNumberMatches.
        Assert.All(matches, match => Assert.Equal(MatchRating.DateMatches, match.Rating));
        Assert.Equal(["101", "102"], matches.Select(match => match.Candidate?.ID.ID));
    }

    [Fact]
    public void Auto_PicksTheFlatPath_WhenTheSourceHasNoSeasonNumbers()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired),
            AnidbEpisode(2, 2, _firstAired.AddDays(7)),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, null, _firstAired),
            ProviderEpisode(102, 2, null, _firstAired.AddDays(7)),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider);

        Assert.All(matches, match => Assert.Equal(MatchRating.DateAndNumberMatches, match.Rating));
    }

    [Fact]
    public void Auto_TreatsSeasonZero_AsASeason()
    {
        var anidb = new List<IAnidbEpisode> { AnidbEpisode(1, 1, _firstAired) };
        var provider = new List<IEpisode> { ProviderEpisode(101, 1, 0, _firstAired) };

        var matches = Matcher().MatchEpisodes(anidb, provider);

        // On the seasoned path a normal episode never reaches into season 0, so
        // this stays unmatched; on the flat path it would have been claimed.
        Assert.Null(Assert.Single(matches).Candidate);
    }

    #endregion

    #region Options and unmatched

    [Fact]
    public void UnmatchedEpisodesComeBackWithNone_WhileTheRestArePlaced()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired),
            AnidbEpisode(2, 2, _firstAired.AddDays(7)),
            AnidbEpisode(3, 1, null, EpisodeType.Special),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, null, _firstAired),
            ProviderEpisode(102, 2, null, _firstAired.AddDays(7)),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateThenNumber });

        Assert.Equal([1, 2, 3], matches.Select(match => match.AnidbEpisode.AnidbID));
        var special = Assert.Single(matches, match => match.AnidbEpisode.Type is EpisodeType.Special);
        Assert.Null(special.Candidate);
        Assert.Equal(MatchRating.None, special.Rating);
        Assert.All(matches.Where(match => match.AnidbEpisode.Type is EpisodeType.Episode), match => Assert.NotNull(match.Candidate));
    }

    [Fact]
    public void IncludeSpecialsFalse_DropsSpecialsFromTheResultEntirely()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired),
            AnidbEpisode(2, 2, _firstAired.AddDays(7)),
            AnidbEpisode(3, 1, _firstAired.AddDays(3), EpisodeType.Special),
            AnidbEpisode(4, 2, _firstAired.AddDays(10), EpisodeType.Special),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, null, _firstAired),
            ProviderEpisode(102, 2, null, _firstAired.AddDays(7)),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions
        {
            Strategy = EpisodeMatchStrategy.DateThenNumber,
            IncludeSpecials = false,
        });

        Assert.Equal(2, matches.Count);
        Assert.DoesNotContain(matches, match => match.AnidbEpisode.Type is EpisodeType.Special);
        Assert.Equal([1, 2], matches.Select(match => match.AnidbEpisode.AnidbID));
    }

    #endregion

    #region Existing links

    // Hidden means don't look for a new match, not drop a manual one; its
    // candidate leaves the pool too, so no neighbour can claim it.
    [Fact]
    public void AnExistingLink_IsKept_OnAHiddenEpisode()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired, title: "Alpha"),
            HiddenAnidbEpisode(3, 1),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired, "Alpha"),
            ProviderEpisode(103, 1, 0, _firstAired.AddDays(3), "Bonus"),
        };

        var matches = Matcher().MatchEpisodes(
            anidb,
            provider,
            [CrossReference(3, "103", MatchRating.UserVerified)],
            new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons }
        );

        var hidden = Assert.Single(matches, match => match.AnidbEpisode.AnidbID is 3);
        Assert.Equal("103", hidden.Candidate?.ID.ID);
        Assert.Equal(MatchRating.UserVerified, hidden.Rating);
    }

    [Fact]
    public void AHiddenEpisode_IsStillPassedOver_WithNoExistingLink()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired, title: "Alpha"),
            HiddenAnidbEpisode(3, 1),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired, "Alpha"),
            ProviderEpisode(103, 1, 0, _firstAired.AddDays(3), "Bonus"),
        };

        var matches = Matcher().MatchEpisodes(
            anidb,
            provider,
            [],
            new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons }
        );

        var hidden = Assert.Single(matches, match => match.AnidbEpisode.AnidbID is 3);
        Assert.Null(hidden.Candidate);
        Assert.Equal(MatchRating.None, hidden.Rating);
    }

    // A link to an entry outside the candidates (another season or series) cannot
    // be answered from them, so saving the result leaves that link alone.
    [Fact]
    public void AnEpisodeSettledOutsideTheCandidates_IsLeftOut()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired, title: "Alpha"),
            AnidbEpisode(2, 2, _firstAired.AddDays(7), title: "Beta"),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 2, _firstAired, "Alpha"),
            ProviderEpisode(102, 2, 2, _firstAired.AddDays(7), "Beta"),
        };

        var matches = Matcher().MatchEpisodes(
            anidb,
            provider,
            [CrossReference(1, "999", MatchRating.DateAndTitleMatches)],
            new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons }
        );

        Assert.DoesNotContain(matches, match => match.AnidbEpisode.AnidbID is 1);
        Assert.Equal("102", Assert.Single(matches).Candidate?.ID.ID);
    }

    [Fact]
    public void AnEpisodeAPersonLinkedToNothing_IsLeftOut()
    {
        var anidb = new List<IAnidbEpisode> { AnidbEpisode(1, 1, _firstAired, title: "Alpha") };
        var provider = new List<IEpisode> { ProviderEpisode(101, 1, 1, _firstAired, "Alpha") };

        var matches = Matcher().MatchEpisodes(
            anidb,
            provider,
            [CrossReference(1, null, MatchRating.UserVerified)],
            new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons }
        );

        Assert.Empty(matches);
    }

    // Only a person settles an episode as having no match, so an automatic
    // link to nothing is matched again.
    [Fact]
    public void AnAutomaticLinkToNothing_IsMatchedAgain()
    {
        var anidb = new List<IAnidbEpisode> { AnidbEpisode(1, 1, _firstAired, title: "Alpha") };
        var provider = new List<IEpisode> { ProviderEpisode(101, 1, 1, _firstAired, "Alpha") };

        var matches = Matcher().MatchEpisodes(
            anidb,
            provider,
            [CrossReference(1, null, MatchRating.DateAndTitleMatches)],
            new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons }
        );

        var match = Assert.Single(matches);
        Assert.Equal("101", match.Candidate?.ID.ID);
        Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating);
    }

    [Fact]
    public void AHiddenEpisode_DropsAnAutomaticLink()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired, title: "Alpha"),
            HiddenAnidbEpisode(3, 1),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired, "Alpha"),
            ProviderEpisode(103, 1, 0, _firstAired.AddDays(3), "Bonus"),
        };

        var matches = Matcher().MatchEpisodes(
            anidb,
            provider,
            [CrossReference(3, "103", MatchRating.DateAndTitleMatches)],
            new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons }
        );

        var hidden = Assert.Single(matches, match => match.AnidbEpisode.AnidbID is 3);
        Assert.Null(hidden.Candidate);
        Assert.Equal(MatchRating.None, hidden.Rating);
    }

    #endregion

    #region Order reconciliation

    // AniDB dates episode 1 by a premiere the source lacks, so episode 2 takes its date.
    // All are weak, so they go back into AniDB order, each rating travelling with its pairing.
    [Fact]
    public void WeakMatchesOutOfOrder_AreSwappedBackIntoAnidbOrder()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired.AddDays(-7)),
            AnidbEpisode(2, 2, _firstAired),
            AnidbEpisode(3, 3, _firstAired.AddDays(7)),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired),
            ProviderEpisode(102, 2, 1, _firstAired.AddDays(7)),
            ProviderEpisode(103, 3, 1, _firstAired.AddDays(14)),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        Assert.Equal(["101", "102", "103"], matches.Select(match => match.Candidate?.ID.ID));
        Assert.Equal([MatchRating.DateMatches, MatchRating.DateMatches, MatchRating.DateKindaMatches], matches.Select(match => match.Rating));
    }

    // Title evidence is trusted over positional guessing, even beside a weak neighbour out of order.
    [Fact]
    public void ReconciliationNeverMovesATitleMatch()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, null, title: "Beta"),
            AnidbEpisode(2, 2, null),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired, "Alpha"),
            ProviderEpisode(102, 2, 1, _firstAired.AddDays(7), "Beta"),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        Assert.Equal(["102", "101"], matches.Select(match => match.Candidate?.ID.ID));
        Assert.Equal([MatchRating.TitleMatches, MatchRating.FirstAvailable], matches.Select(match => match.Rating));
    }

    // One left-to-right pass only moves one inversion along, so reconciliation
    // has to repeat until nothing moves.
    [Fact]
    public void AThreeEpisodeReversal_IsFullyUntangled()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired.AddDays(14)),
            AnidbEpisode(2, 2, _firstAired.AddDays(7)),
            AnidbEpisode(3, 3, _firstAired),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired),
            ProviderEpisode(102, 2, 1, _firstAired.AddDays(7)),
            ProviderEpisode(103, 3, 1, _firstAired.AddDays(14)),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        Assert.Equal(["101", "102", "103"], matches.Select(match => match.Candidate?.ID.ID));
    }

    [Fact]
    public void SpecialsAndEpisodes_AreNeverReconciledAgainstEachOther()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired.AddDays(7)),
            AnidbEpisode(2, 1, _firstAired.AddDays(3), EpisodeType.Special),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired.AddYears(-3)),
            ProviderEpisode(102, 2, 1, _firstAired.AddDays(7)),
            ProviderEpisode(103, 1, 0, _firstAired.AddDays(3)),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        Assert.Equal("102", Assert.Single(matches, match => match.AnidbEpisode.Type is EpisodeType.Episode).Candidate?.ID.ID);
        Assert.Equal("103", Assert.Single(matches, match => match.AnidbEpisode.Type is EpisodeType.Special).Candidate?.ID.ID);
    }

    #endregion

    #region Air-date fallback

    [Fact]
    public void AnEpisodeWithNothingInTheStrictWindow_TakesTheNearestAirDate()
    {
        var anidb = new List<IAnidbEpisode>
        {
            AnidbEpisode(1, 1, _firstAired),
            AnidbEpisode(2, 2, _firstAired.AddDays(7)),
        };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 2, _firstAired),
            ProviderEpisode(102, 2, 2, _firstAired.AddDays(27)),
            ProviderEpisode(103, 3, 2, _firstAired.AddDays(12)),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        var second = Assert.Single(matches, match => match.AnidbEpisode.AnidbID is 2);
        Assert.Equal("103", second.Candidate?.ID.ID);
        Assert.Equal(MatchRating.DateKindaMatches, second.Rating);
    }

    // For a special with no title to go on the loose fallback is little more
    // than a coin flip, so it is not taken.
    [Fact]
    public void ASpecial_NeverTakesTheLooseAirDateFallback()
    {
        var anidb = new List<IAnidbEpisode> { AnidbEpisode(2, 1, _firstAired, EpisodeType.Special) };
        var provider = new List<IEpisode> { ProviderEpisode(202, 1, 0, _firstAired.AddDays(5)) };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        var special = Assert.Single(matches);
        Assert.Null(special.Candidate);
        Assert.Equal(MatchRating.None, special.Rating);
    }

    [Fact]
    public void AnOrdinaryEpisode_TakesTheLooseAirDateFallback()
    {
        var anidb = new List<IAnidbEpisode> { AnidbEpisode(2, 2, _firstAired) };
        var provider = new List<IEpisode> { ProviderEpisode(202, 3, 2, _firstAired.AddDays(5)) };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        var episode = Assert.Single(matches);
        Assert.Equal("202", episode.Candidate?.ID.ID);
        Assert.Equal(MatchRating.DateKindaMatches, episode.Rating);
    }

    [Fact]
    public void AnOva_ReachesForAnOrdinaryEpisodeBeforeASpecial()
    {
        var ova = Anime(AnimeType.OVA);
        var anidb = new List<IAnidbEpisode> { AnidbEpisode(1, 1, null, series: ova) };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(100, 1, 0, null),
            ProviderEpisode(101, 1, 1, null),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        var episode = Assert.Single(matches);
        Assert.Equal("101", episode.Candidate?.ID.ID);
        Assert.Equal(MatchRating.FirstAvailable, episode.Rating);
    }

    #endregion

    #region Titles

    [Fact]
    public void TitleCandidates_KeepEnglishAndTheOriginalLanguage_AndDropTheRest()
    {
        var episode = ProviderEpisode(1, 3, 1, null, titles:
        [
            Title("The Lost Village", TestSources.Plugin, "en", "US"),
            Title("失われた村", TestSources.Plugin, "ja", ""),
            Title("失去的村子", TestSources.Plugin, "zh", "CN"),
        ]);

        var candidates = MetadataMatchingEngine.TitleCandidatesOf(episode, "ja");

        Assert.Contains("The Lost Village", candidates);
        Assert.Contains("失われた村", candidates);
        Assert.DoesNotContain("失去的村子", candidates);
    }

    [Fact]
    public void TitleCandidates_KeepEveryLanguage_WithNoOriginalLanguage()
    {
        var episode = ProviderEpisode(1, 3, 1, null, titles:
        [
            Title("The Lost Village", TestSources.Plugin, "en", "US"),
            Title("失去的村子", TestSources.Plugin, "zh", "CN"),
        ]);

        var candidates = MetadataMatchingEngine.TitleCandidatesOf(episode, null);

        Assert.Equal(["The Lost Village", "失去的村子"], candidates);
    }

    [Fact]
    public void TitleCandidates_DropThePlaceholderInEveryLanguage()
    {
        var episode = ProviderEpisode(2, 3, 1, null, titles:
        [
            Title("Episode 3", TestSources.Plugin, "en", "US"),
            Title("Episode 3", TestSources.Plugin, "ja", ""),
        ]);

        Assert.Empty(MetadataMatchingEngine.TitleCandidatesOf(episode, "ja"));
    }

    [Fact]
    public void ATitleInAnUnrelatedLanguage_DoesNotPlaceAnEpisode()
    {
        var anidb = new List<IAnidbEpisode> { AnidbEpisode(1, 1, null, title: "Lost Village") };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, null, titles: [Title("Lost Village", TestSources.Plugin, "zh", "CN")], originalLanguage: "ja"),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        Assert.Equal(MatchRating.FirstAvailable, Assert.Single(matches).Rating);
    }

    // AniDB's generic English titles are no longer stored; the episodes pair as they did with them.
    [Fact]
    public void AnidbEpisodesWithoutTheirGenericTitles_PairAsBefore()
    {
        IReadOnlyList<IAnidbEpisode> Anidb(bool withGenericTitles)
        {
            return
            [
                AnidbEpisode(1, 1, _firstAired, title: withGenericTitles ? "Episode 1" : null),
                AnidbEpisode(2, 2, _firstAired.AddDays(7), title: withGenericTitles ? "Episode 2" : null),
                AnidbEpisode(3, 1, _firstAired.AddDays(14), EpisodeType.Special, withGenericTitles ? "Episode S1" : null),
            ];
        }
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, _firstAired, "Alpha"),
            ProviderEpisode(102, 2, 1, _firstAired.AddDays(7), "Beta"),
            ProviderEpisode(103, 1, 0, _firstAired.AddDays(14), "Gamma"),
        };
        var options = new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons };

        var before = Matcher().MatchEpisodes(Anidb(true), provider, options: options);
        var after = Matcher().MatchEpisodes(Anidb(false), provider, options: options);

        Assert.Equal(["101", "102", "103"], after.Select(match => match.Candidate?.ID.ID));
        Assert.Equal(before.Select(match => (match.Candidate?.ID.ID, match.Rating)), after.Select(match => (match.Candidate?.ID.ID, match.Rating)));
    }

    // A title another source put on the AniDB entry cannot vouch for a match with itself.
    [Fact]
    public void OnlyTheTitlesAnidbGave_AreSearchedWith()
    {
        var anidb = new List<IAnidbEpisode> { AnidbEpisode(1, 1, null, titles: [Title("Beta", TestSources.Plugin)]) };
        var provider = new List<IEpisode>
        {
            ProviderEpisode(101, 1, 1, null, "Alpha"),
            ProviderEpisode(102, 2, 1, null, "Beta"),
        };

        var matches = Matcher().MatchEpisodes(anidb, provider, options: new EpisodeMatchOptions { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });

        var match = Assert.Single(matches);
        Assert.Equal("101", match.Candidate?.ID.ID);
        Assert.Equal(MatchRating.FirstAvailable, match.Rating);
    }

    #endregion

    #region Series

    [Fact]
    public void MatchSeries_TakesTheTitleAndYear_AndSaysWhyTheOthersLost()
    {
        var anime = SeriesAnime(new(2020, 4, 3), 12);
        var matches = Matcher().MatchSeries(anime,
        [
            Series("1", "Kaguya", aired: new(2015, 1, 1)),
            Series("2", "Kaguya", aired: new(2020, 4, 3)),
            Series("3", "Something Else", aired: new(2011, 1, 1)),
        ], new() { Query = "Kaguya" });

        Assert.Equal(["2", "1", "3"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal([MatchRating.DateAndTitleMatches, MatchRating.TitleMatches, MatchRating.None], matches.Select(match => match.Rating));
        Assert.Equal([MatchRejectionReason.None, MatchRejectionReason.DateMismatch, MatchRejectionReason.TitleMismatch], matches.Select(match => match.Rejection));
    }

    // Unlike the episode ratings' score, a title alone beats a date alone.
    [Fact]
    public void MatchSeries_PutsATitleAheadOfADate()
    {
        var matches = Matcher().MatchSeries(SeriesAnime(new(2020, 4, 3), 12),
        [
            Series("1", "Unrelated", aired: new(2020, 1, 1)),
            Series("2", "Kaguya", aired: new(2015, 1, 1)),
        ], new() { Query = "Kaguya" });

        Assert.Equal(["2", "1"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal(MatchRejectionReason.TitleMismatch, matches[1].Rejection);
    }

    [Fact]
    public void MatchSeries_SettlesATieOnEpisodeCountThenOrder()
    {
        var matches = Matcher().MatchSeries(SeriesAnime(new(2020, 4, 3), 12),
        [
            Series("1", "Kaguya", aired: new(2020, 4, 3), seasons: [Season(1, 24, new(2020, 4, 3))]),
            Series("2", "Kaguya", aired: new(2020, 4, 3), seasons: [Season(1, 13, new(2020, 4, 3))]),
            Series("3", "Kaguya", aired: new(2020, 4, 3), seasons: [Season(1, 13, new(2020, 4, 3))]),
        ], new() { Query = "Kaguya" });

        Assert.Equal(["2", "3", "1"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal([MatchRejectionReason.None, MatchRejectionReason.Outranked, MatchRejectionReason.EpisodeCountMismatch], matches.Select(match => match.Rejection));
        Assert.Equal(1, matches[0].SeasonNumber);
    }

    [Fact]
    public void MatchSeries_AHintSettlesATieBeforeTheEpisodeCount()
    {
        var matches = Matcher().MatchSeries(SeriesAnime(new(2020, 4, 3), 12),
        [
            Series("1", "Kaguya", aired: new(2020, 4, 3), seasons: [Season(1, 12, new(2020, 4, 3))]),
            Series("2", "Kaguya", aired: new(2020, 4, 3), seasons: [Season(1, 24, new(2020, 4, 3))]),
        ], new() { Query = "Kaguya", HintedIDs = [new(TestSources.Plugin, MetadataEntityType.Series, "2")] });

        Assert.Equal(["2", "1"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal([MatchRejectionReason.None, MatchRejectionReason.Outranked], matches.Select(match => match.Rejection));
    }

    [Fact]
    public void MatchSeries_AHintNeverBeatsABetterRating()
    {
        var matches = Matcher().MatchSeries(SeriesAnime(new(2020, 4, 3), 12),
        [
            Series("1", "Kaguya", aired: new(2020, 4, 3)),
            Series("2", "Kaguya", aired: new(2015, 1, 1)),
        ], new() { Query = "Kaguya", HintedIDs = [new(TestSources.Plugin, MetadataEntityType.Series, "2")] });

        Assert.Equal(["1", "2"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal([MatchRejectionReason.None, MatchRejectionReason.DateMismatch], matches.Select(match => match.Rejection));
    }

    // The season that lines up best carries the date: a show that started
    // years earlier still agrees through the season the anime is.
    [Fact]
    public void MatchSeries_DatesTheBestSeason()
    {
        var matches = Matcher().MatchSeries(SeriesAnime(new(2021, 1, 9), 10),
        [
            Series("1", "Shingeki no Kyojin", aired: new(2013, 4, 7), seasons: [Season(1, 25, new(2013, 4, 7)), Season(4, 10, new(2021, 1, 9))]),
        ], new() { Query = "Shingeki no Kyojin" });

        var match = Assert.Single(matches);
        Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating);
        Assert.Equal(4, match.SeasonNumber);
    }

    // Across new year, first episodes airing within three days still agree,
    // but only once the source has the first episode's date.
    [Fact]
    public void MatchSeries_FallsBackOnTheFirstEpisodeAcrossNewYear()
    {
        var anime = SeriesAnime(new(2020, 1, 1), 12);
        MetadataSeriesSearchResult Candidate(DateOnly? firstEpisode)
            => Series("1", "Kaguya", aired: new(2015, 1, 1), seasons: [new() { SeasonNumber = 2, EpisodeCount = 12, FirstAiredAt = new(2019, 12, 30), FirstEpisodeAiredAt = firstEpisode }]);

        Assert.Equal(MatchRating.TitleMatches, Matcher().MatchSeries(anime, [Candidate(null)], new() { Query = "Kaguya" })[0].Rating);
        Assert.Equal(MatchRating.DateAndTitleMatches, Matcher().MatchSeries(anime, [Candidate(new(2019, 12, 30))], new() { Query = "Kaguya" })[0].Rating);
        Assert.Equal(MatchRating.TitleMatches, Matcher().MatchSeries(anime, [Candidate(new(2019, 12, 20))], new() { Query = "Kaguya" })[0].Rating);
    }

    [Fact]
    public void MatchSeries_UsesTheRegularBroadcastDate()
    {
        var anime = SeriesAnime(new(2019, 12, 15), 12, regularAirDate: new(2020, 1, 5));
        var match = Assert.Single(Matcher().MatchSeries(anime, [Series("1", "Kaguya", aired: new(2020, 1, 5))], new() { Query = "Kaguya" }));
        Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating);
    }

    // The query without its subtitle only ever matches closely, so a parent
    // show cannot outrank the entry matching the full title.
    [Fact]
    public void MatchSeries_TreatsTheTitleWithoutSubtitleAsClose()
    {
        var matches = Matcher().MatchSeries(SeriesAnime(new(2024, 7, 7), 25),
        [
            Series("1", "Fairy Tail", aired: new(2009, 10, 12)),
            Series("2", "Fairy Tail: 100 Years Quest", aired: new(2024, 7, 7)),
        ], new() { Query = "Fairy Tail: 100 Years Quest" });

        Assert.Equal(["2", "1"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal(MatchRating.TitleKindaMatches, matches[1].Rating);
    }

    // Where every season is an entry of its own, the bare title names season
    // one, so a title found only without its sequel suffix needs the date.
    [Theory]
    [InlineData(false, MatchRating.TitleMatches)]
    [InlineData(true, MatchRating.TitleKindaMatches)]
    public void MatchSeries_HoldsBackASuffixOnlyTitle_WhereSeasonsAreSeparate(bool seasonsAreSeparate, MatchRating expected)
    {
        var match = Assert.Single(Matcher().MatchSeries(SeriesAnime(new(2022, 4, 1), 12),
            [Series("1", "Kaguya", aired: new(2019, 1, 12))],
            new() { Query = "Kaguya Season 2", SeasonsAreSeparateEntries = seasonsAreSeparate }));
        Assert.Equal(expected, match.Rating);
    }

    // The same holds for a Japanese title numbering its season with 第…期,
    // in digits or in kanji numerals.
    [Theory]
    [InlineData("かぐや様は告らせたい 第2期", false, MatchRating.TitleMatches)]
    [InlineData("かぐや様は告らせたい 第2期", true, MatchRating.TitleKindaMatches)]
    [InlineData("かぐや様は告らせたい 第二期", false, MatchRating.TitleMatches)]
    [InlineData("かぐや様は告らせたい 第二期", true, MatchRating.TitleKindaMatches)]
    public void MatchSeries_HoldsBackAJapaneseSuffixOnlyTitle_WhereSeasonsAreSeparate(string query, bool seasonsAreSeparate, MatchRating expected)
    {
        var match = Assert.Single(Matcher().MatchSeries(SeriesAnime(new(2022, 4, 1), 12),
            [Series("1", "かぐや様は告らせたい", aired: new(2019, 1, 12))],
            new() { Query = query, QueryLanguage = TitleLanguage.Japanese, SeasonsAreSeparateEntries = seasonsAreSeparate }));
        Assert.Equal(expected, match.Rating);
    }

    // Two titles apart only by punctuation the fold keeps, or by the CJK
    // form of a mark it folds, name the same work.
    [Theory]
    [InlineData("猫田のことが気になって仕方ない.", "猫田のことが気になって仕方ない。")]
    [InlineData("エクセル・サーガ", "エクセルサーガ")]
    [InlineData("D·N·A²", "DNA²")]
    [InlineData("Don’t Toy with Me", "Don't Toy with Me")]
    [InlineData("【推しの子】", "推しの子")]
    [InlineData("Hanawa Hekonai – The Kappa Festival", "Hanawa Hekonai - The Kappa Festival")]
    public void MatchSeries_IgnoresPunctuationWhenComparingTitlesWhole(string query, string name)
    {
        var match = Assert.Single(Matcher().MatchSeries(SeriesAnime(new(2019, 1, 12), 12),
            [Series("1", name, aired: new(2019, 1, 12))],
            new() { Query = query }));
        Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating);
    }

    // A '?' or an apostrophe may be all that sets a sequel's title apart, so
    // it still counts when comparing titles whole.
    [Theory]
    [InlineData("Gochuumon wa Usagi Desu ka??", "Gochuumon wa Usagi Desu ka?")]
    [InlineData("WORKING'!!", "WORKING!!")]
    [InlineData("Kaguya-sama wa Kokurasetai? Tensai-tachi no Renai Zunousen", "Kaguya-sama wa Kokurasetai: Tensai-tachi no Renai Zunousen")]
    public void MatchSeries_KeepsTheMarksThatSetASequelApart(string query, string name)
    {
        var match = Assert.Single(Matcher().MatchSeries(SeriesAnime(new(2019, 1, 12), 12),
            [Series("1", name, aired: new(2019, 1, 12))],
            new() { Query = query }));
        Assert.NotEqual(MatchRating.DateAndTitleMatches, match.Rating);
        Assert.NotEqual(MatchRating.TitleMatches, match.Rating);
    }

    [Fact]
    public void MatchSeries_DoesNotTakeATitleInsideALongerNameAsExact()
    {
        var match = Assert.Single(Matcher().MatchSeries(SeriesAnime(new(2017, 4, 5), 12),
            [Series("1", "Boruto: Naruto Next Generations", aired: new(2017, 4, 5))],
            new() { Query = "Naruto" }));
        Assert.NotEqual(MatchRating.DateAndTitleMatches, match.Rating);
        Assert.NotEqual(MatchRating.TitleMatches, match.Rating);
    }

    [Fact]
    public void MatchSeries_TriesEveryTitleWithoutAQuery()
    {
        var anime = SeriesAnime(new(2020, 4, 3), 12, titles: ["Kaguya-sama wa Kokurasetai", "Kaguya-sama: Love is War"]);
        var match = Assert.Single(Matcher().MatchSeries(anime, [Series("1", "Something", alternateTitles: ["Kaguya-sama: Love Is War"], aired: new(2020, 4, 3))]));
        Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating);
        Assert.Equal(MatchRejectionReason.None, match.Rejection);
    }

    // Without a query, a short title or synonym must name the candidate exactly,
    // while the same prefix of a main title still counts.
    [Theory]
    [InlineData(TitleType.Short, MatchRating.DateMatches)]
    [InlineData(TitleType.Synonym, MatchRating.DateMatches)]
    [InlineData(TitleType.Main, MatchRating.DateAndTitleKindaMatches)]
    [InlineData(TitleType.Official, MatchRating.DateAndTitleKindaMatches)]
    public void MatchSeries_WithoutAQuery_TakesShortTitlesOnlyExactly(TitleType type, MatchRating expected)
    {
        var anime = SeriesAnime(new(2006, 4, 3), 25);
        Mock.Get(anime).SetupGet(entry => entry.Titles).Returns([Title("Air", MetadataSource.AniDB, "x-jat", "", type)]);
        var match = Assert.Single(Matcher().MatchSeries(anime, [Series("1", "Air Gear", aired: new(2006, 4, 11))]));
        Assert.Equal(expected, match.Rating);

        // Another year leaves nothing agreeing at all.
        var other = Assert.Single(Matcher().MatchSeries(anime, [Series("1", "Air Gear", aired: new(2011, 4, 11))]));
        Assert.Equal(type is TitleType.Short or TitleType.Synonym ? MatchRating.None : MatchRating.TitleKindaMatches, other.Rating);
    }

    // An exact short title still counts in full, but not once a sequel
    // suffix is dropped from it; a query is judged as it always was.
    [Fact]
    public void MatchSeries_WithoutAQuery_StillTakesAnExactShortTitle()
    {
        var anime = SeriesAnime(new(2006, 4, 3), 25);
        Mock.Get(anime).SetupGet(entry => entry.Titles).Returns(
        [
            Title("Air", MetadataSource.AniDB, "x-jat", "", TitleType.Short),
            Title("Kaguya 2", MetadataSource.AniDB, "x-jat", "", TitleType.Synonym),
        ]);
        Assert.Equal(MatchRating.DateAndTitleMatches, Assert.Single(Matcher().MatchSeries(anime, [Series("1", "Air", aired: new(2006, 4, 11))])).Rating);
        Assert.Equal(MatchRating.DateMatches, Assert.Single(Matcher().MatchSeries(anime, [Series("1", "Kaguya", aired: new(2006, 4, 11))])).Rating);
        Assert.Equal(MatchRating.DateAndTitleKindaMatches,
            Assert.Single(Matcher().MatchSeries(anime, [Series("1", "Air Gear", aired: new(2006, 4, 11))], new() { Query = "Air" })).Rating);
    }

    // Adult entries are rejected unless allowed, and a music video is never
    // taken for anything else, however well they match.
    [Fact]
    public void MatchSeries_RejectsRestrictedEntriesAndMusicVideos()
    {
        var candidates = new List<MetadataSeriesSearchResult>
        {
            Series("1", "Kaguya", aired: new(2020, 4, 3)) with { IsRestricted = true },
            Series("2", "Kaguya", aired: new(2020, 4, 3)) with { Type = AnimeType.MusicVideo },
            Series("3", "Kaguya", aired: new(2015, 4, 3)),
        };

        var matches = Matcher().MatchSeries(SeriesAnime(new(2020, 4, 3), 12), candidates, new() { Query = "Kaguya" });
        Assert.Equal(["3", "1", "2"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal([MatchRejectionReason.None, MatchRejectionReason.Restricted, MatchRejectionReason.TypeMismatch], matches.Select(match => match.Rejection));

        var allowed = Matcher().MatchSeries(SeriesAnime(new(2020, 4, 3), 12), candidates, new() { Query = "Kaguya", IncludeRestricted = true });
        Assert.Equal("1", allowed[0].Candidate.ID.ID);
    }

    [Fact]
    public void MatchSeries_TakesNothingWhenNothingAgrees()
    {
        var match = Assert.Single(Matcher().MatchSeries(SeriesAnime(new(2020, 4, 3), 12), [Series("1", "Unrelated", aired: new(2011, 1, 1))], new() { Query = "Kaguya" }));
        Assert.Equal(MatchRating.None, match.Rating);
        Assert.Equal(MatchRejectionReason.TitleMismatch, match.Rejection);
    }

    // A show begun after the anime ended cannot hold it, so the long-running show
    // wins (as for the 2019 Pocket Monsters against Horizons).
    [Fact]
    public void MatchSeries_TurnsDownASeriesBegunAfterTheAnimeEnded()
    {
        var anime = SeriesAnime(new(2019, 11, 17), 136, endDate: new(2022, 12, 16));
        var matches = Matcher().MatchSeries(anime,
        [
            Series("220150", "Pocket Monsters", aired: new(2023, 4, 14), seasons: [Season(1, 152, new(2023, 4, 14))]),
            Series("60572", "Pocket Monsters", aired: new(1997, 4, 1), seasons: [Season(1, 82, new(1997, 4, 1)), Season(23, 48, new(2019, 11, 17))]),
        ], new() { Query = "Pocket Monsters" });

        Assert.Equal(["60572", "220150"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal([MatchRejectionReason.None, MatchRejectionReason.DateMismatch], matches.Select(match => match.Rejection));
        Assert.Null(matches[0].Details);
    }

    // Beginning after the anime ended only settles a tie: a show matching on its own
    // (the prologue shown ahead of it being one of its specials) is still taken.
    [Theory]
    [InlineData(2016, 4, 2, true)]
    [InlineData(2019, 4, 2, true)]
    [InlineData(2019, 4, 2, false)]
    public void MatchSeries_TakesALaterSeriesMatchingOnItsOwn(int year, int month, int day, bool ended)
    {
        var anime = SeriesAnime(new(2016, 3, 17), 1, endDate: ended ? new(2016, 3, 17) : null);
        var match = Assert.Single(Matcher().MatchSeries(anime, [Series("1", "Gyakuten Saiban", aired: new(year, month, day))], new() { Query = "Gyakuten Saiban" }));
        Assert.Equal(MatchRejectionReason.None, match.Rejection);
    }

    // A title spelt with or without spaces between Japanese words, or with
    // the ideographic comma for a comma, is the same title.
    [Theory]
    [InlineData("くま クマ 熊 ベアー", "くまクマ熊ベアー")]
    [InlineData("悶えてよ, アダムくん", "悶えてよ、アダムくん")]
    public void MatchSeries_TakesAJapaneseTitleSpeltEitherWay(string query, string name)
    {
        var match = Assert.Single(Matcher().MatchSeries(SeriesAnime(new(2020, 11, 7), 12), [Series("1", name, aired: new(2020, 11, 7))], new() { Query = query, QueryLanguage = TitleLanguage.Japanese }));
        Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating);
    }

    // Dropping those spaces is for comparing whole titles only: a show whose
    // name merely begins with the title is still no more than close.
    [Fact]
    public void MatchSeries_KeepsWordBoundariesForAJapanesePrefix()
    {
        var matches = Matcher().MatchSeries(SeriesAnime(new(2019, 11, 17), 48),
        [
            Series("1", "ポケットモンスター サイドストーリー", aired: new(2002, 12, 3)),
            Series("2", "ポケットモンスターサイドストーリー", aired: new(2002, 12, 3)),
        ], new() { Query = "ポケットモンスター", QueryLanguage = TitleLanguage.Japanese });

        Assert.Equal([MatchRating.TitleKindaMatches, MatchRating.None], matches.Select(match => match.Rating));
    }

    #endregion

    #region Episode alignment

    // A split cour: the second half of a 24-episode season begun the year before,
    // which neither the years nor the episode count would pick.
    [Fact]
    public void MatchSeries_AlignsASplitCourWithinItsSeason()
    {
        var anime = SeriesAnime(new(2021, 1, 2), 12);
        MetadataSeriesSearchResult Candidate(bool withEpisodes)
            => Series("1", "Kaguya", aired: new(2020, 10, 10), seasons:
            [
                Season(1, 24, new(2020, 10, 10)) with { Episodes = withEpisodes ? Weekly(new(2020, 10, 10), 1, 24) : null },
                Season(2, 12, new(2022, 1, 8)),
            ]);

        var without = Assert.Single(Matcher().MatchSeries(anime, [Candidate(false)], new() { Query = "Kaguya" }));
        var with = Assert.Single(Matcher().MatchSeries(anime, [Candidate(true)], new() { Query = "Kaguya" }));

        Assert.Equal((MatchRating.TitleMatches, 2), (without.Rating, without.SeasonNumber));
        Assert.Null(without.EpisodeAlignment);
        Assert.Equal((MatchRating.DateAndTitleMatches, 1), (with.Rating, with.SeasonNumber));
        Assert.Equal(new EpisodeAlignment { SeasonNumber = 1, Offset = 12, MatchedEpisodes = 12, DatedEpisodes = 12, MatchedDays = 12, IsConclusive = true }, with.EpisodeAlignment);
    }

    // A day either way still lines up, as two sources often date a late-night
    // broadcast apart.
    [Fact]
    public void MatchSeries_AlignsEpisodesADayApart()
    {
        var anime = SeriesAnime(new(2021, 1, 2), 12);
        var match = Assert.Single(Matcher().MatchSeries(anime,
            [Series("1", "Kaguya", aired: new(2020, 10, 10), seasons: [Season(1, 24, new(2020, 10, 10)) with { Episodes = Weekly(new(2020, 10, 11), 1, 24) }])],
            new() { Query = "Kaguya" }));

        Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating);
        Assert.Equal((12, 12), (match.EpisodeAlignment?.Offset, match.EpisodeAlignment?.MatchedEpisodes));
    }

    // A double premiere counts as one day, and a source keeping it as one
    // episode lines the rest up one apart rather than being outvoted by it.
    [Fact]
    public void MatchSeries_CountsASameDayDoubleOnce()
    {
        var start = new DateOnly(2021, 1, 2);
        var anime = DatedAnime([(1, start), .. Enumerable.Range(2, 11).Select(number => (number, start.AddDays(7 * (number - 2))))]);
        var doubled = Series("1", "Kaguya", aired: new(2020, 1, 4), seasons:
        [
            Season(1, 12, new(2020, 1, 4)) with { Episodes = [Episode(1, start), .. Weekly(start, 2, 11)] },
        ]);
        var single = Series("2", "Kaguya", aired: new(2020, 1, 4), seasons:
        [
            Season(1, 11, new(2020, 1, 4)) with { Episodes = Weekly(start, 1, 11) },
        ]);

        var matches = Matcher().MatchSeries(anime, [doubled, single], new() { Query = "Kaguya" });

        var both = matches.ToDictionary(match => match.Candidate.ID.ID, match => match.EpisodeAlignment);
        Assert.Equal((0, 12, 11), (both["1"]!.Offset, both["1"]!.MatchedEpisodes, both["1"]!.MatchedDays));
        Assert.Equal((-1, 11, 11, true), (both["2"]!.Offset, both["2"]!.MatchedEpisodes, both["2"]!.MatchedDays, both["2"]!.IsConclusive));
        Assert.All(matches, match => Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating));
    }

    // Too few days lined up counts for nothing, unless the anime has no more
    // dated days than that.
    [Fact]
    public void MatchSeries_NeedsEnoughDaysToAlign()
    {
        var start = new DateOnly(2021, 1, 2);
        var sparse = Series("1", "Kaguya", aired: new(2020, 10, 10), seasons:
        [
            Season(1, 24, new(2020, 10, 10)) with { Episodes = [Episode(13, start), Episode(14, start.AddDays(7))] },
        ]);

        var dense = Assert.Single(Matcher().MatchSeries(SeriesAnime(start, 12), [sparse], new() { Query = "Kaguya" }));
        var few = Assert.Single(Matcher().MatchSeries(DatedAnime([(1, start), (2, start.AddDays(7))]), [sparse], new() { Query = "Kaguya" }));

        Assert.Equal(MatchRating.TitleMatches, dense.Rating);
        Assert.False(dense.EpisodeAlignment?.IsConclusive);
        Assert.Equal(MatchRating.DateAndTitleMatches, few.Rating);
        Assert.True(few.EpisodeAlignment?.IsConclusive);
    }

    // Coverage over the stretch, not a count: a long-running show is no better for
    // its length, and the episode count still settles which one is the anime.
    [Fact]
    public void MatchSeries_DoesNotFavourALongShowForItsLength()
    {
        var anime = SeriesAnime(new(2021, 1, 2), 12);
        var matches = Matcher().MatchSeries(anime,
        [
            Series("1", "Kaguya", aired: new(2012, 1, 7), seasons: [Season(1, 500, new(2012, 1, 7)) with { Episodes = Weekly(new(2012, 1, 7), 1, 500) }]),
            Series("2", "Kaguya", aired: new(2021, 1, 2), seasons: [Season(1, 12, new(2021, 1, 2)) with { Episodes = Weekly(new(2021, 1, 2), 1, 12) }]),
        ], new() { Query = "Kaguya" });

        Assert.Equal(["2", "1"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal(1, matches[0].EpisodeAlignment?.Coverage);
        Assert.Equal(1, matches[1].EpisodeAlignment?.Coverage);
        Assert.Equal(MatchRejectionReason.EpisodeCountMismatch, matches[1].Rejection);
    }

    // A remake shares the title, but only the one airing on the anime's days
    // is the anime, even undated but for its episodes.
    [Fact]
    public void MatchSeries_TellsARemakeByItsDates()
    {
        var anime = SeriesAnime(new(2019, 4, 6), 12);
        var matches = Matcher().MatchSeries(anime,
        [
            Series("1", "Fruits Basket", aired: new(2001, 7, 5), seasons: [Season(1, 12, new(2001, 7, 5)) with { Episodes = Weekly(new(2001, 7, 5), 1, 12) }]),
            Series("2", "Fruits Basket", seasons: [new() { SeasonNumber = 1, EpisodeCount = 12, Episodes = Weekly(new(2019, 4, 6), 1, 12) }]),
        ], new() { Query = "Fruits Basket" });

        Assert.Equal(["2", "1"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal(MatchRating.DateAndTitleMatches, matches[0].Rating);
        Assert.Equal((MatchRating.TitleMatches, MatchRejectionReason.DateMismatch), (matches[1].Rating, matches[1].Rejection));
    }

    // An episode the season lists undated tells nothing either way, so a
    // season missing a few dates in the middle still lines up.
    [Fact]
    public void MatchSeries_UndatedEpisodesDoNotCountAgainstTheAlignment()
    {
        var anime = SeriesAnime(new(2021, 1, 2), 12);
        var episodes = Weekly(new(2021, 1, 2), 1, 12)
            .Select(episode => episode.EpisodeNumber is >= 5 and <= 7 ? episode with { AiredAt = null } : episode)
            .ToList();
        var matches = Matcher().MatchSeries(anime,
        [
            Series("1", "Kaguya", aired: new(2020, 10, 10), seasons: [Season(1, 12, new(2020, 10, 10)) with { Episodes = episodes }]),
        ], new() { Query = "Kaguya" });

        var match = Assert.Single(matches);
        Assert.True(match.EpisodeAlignment?.IsConclusive);
        Assert.Equal((9, 9), (match.EpisodeAlignment?.MatchedEpisodes, match.EpisodeAlignment?.DatedEpisodes));
    }

    // Half the episodes on the anime's days is no evidence.
    [Fact]
    public void MatchSeries_AWeakAlignmentChangesNothing()
    {
        var anime = SeriesAnime(new(2021, 1, 2), 12);
        var episodes = Weekly(new(2021, 1, 2), 1, 12).Select(episode => episode.EpisodeNumber > 6 ? episode with { AiredAt = episode.AiredAt!.Value.AddDays(3) } : episode).ToList();
        var matches = Matcher().MatchSeries(anime,
        [
            Series("1", "Kaguya", aired: new(2021, 1, 2)),
            Series("2", "Kaguya", aired: new(2020, 10, 10), seasons: [Season(1, 12, new(2020, 10, 10)) with { Episodes = episodes }]),
        ], new() { Query = "Kaguya" });

        var weak = Assert.Single(matches, match => match.Candidate.ID.ID is "2");
        Assert.Equal(MatchRating.TitleMatches, weak.Rating);
        Assert.False(weak.EpisodeAlignment?.IsConclusive);
        Assert.Equal((6, 12), (weak.EpisodeAlignment?.MatchedEpisodes, weak.EpisodeAlignment?.DatedEpisodes));
    }

    // Between two rated alike, the one whose episodes aired on the anime's
    // days wins before the episode count and the source's order do.
    [Fact]
    public void MatchSeries_AnAlignmentSettlesATie()
    {
        var anime = SeriesAnime(new(2021, 1, 2), 12);
        var matches = Matcher().MatchSeries(anime,
        [
            Series("1", "Kaguya", aired: new(2021, 1, 2), seasons: [Season(1, 12, new(2021, 1, 2))]),
            Series("2", "Kaguya", aired: new(2021, 1, 2), seasons: [Season(1, 13, new(2021, 1, 2)) with { Episodes = Weekly(new(2021, 1, 2), 1, 13) }]),
        ], new() { Query = "Kaguya" });

        Assert.Equal(["2", "1"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal(MatchRejectionReason.DateMismatch, matches[1].Rejection);
        Assert.True(matches[0].EpisodeAlignment?.IsConclusive);
    }

    #endregion

    #region Films

    [Fact]
    public void MatchMovies_AgreesOnAnyReleaseYear()
    {
        var (anime, episode) = FilmAnime(new(2016, 8, 26), 1);
        var match = Assert.Single(Matcher().MatchMovies(anime, episode,
            [Movie("1", "Kimi no Na wa.", released: new(2017, 4, 7), otherReleases: [new(2016, 8, 26)])],
            new() { Query = "Kimi no Na wa." }));
        Assert.Equal(MatchRating.DateAndTitleMatches, match.Rating);
    }

    // The anime's only entry is dated by the anime; one of several films by
    // its own episode.
    [Fact]
    public void MatchMovies_DatesTheFilmByTheAnimeOrTheEpisode()
    {
        var (single, only) = FilmAnime(new(2016, 8, 26), 1, episodeDate: new(2017, 1, 1));
        Assert.Equal(MatchRating.DateAndTitleMatches, Matcher().MatchMovies(single, only, [Movie("1", "Film", released: new(2016, 1, 1))], new() { Query = "Film" })[0].Rating);

        var (series, second) = FilmAnime(new(2016, 8, 26), 2, episodeDate: new(2017, 1, 1));
        Assert.Equal(MatchRating.TitleMatches, Matcher().MatchMovies(series, second, [Movie("1", "Film", released: new(2016, 1, 1))], new() { Query = "Film" })[0].Rating);
        Assert.Equal(MatchRating.DateAndTitleMatches, Matcher().MatchMovies(series, second, [Movie("1", "Film", released: new(2017, 1, 1))], new() { Query = "Film" })[0].Rating);
    }

    [Fact]
    public void MatchMovies_AHintSettlesATie()
    {
        var (anime, episode) = FilmAnime(new(2016, 8, 26), 1);
        var matches = Matcher().MatchMovies(anime, episode,
            [Movie("1", "Film", released: new(2016, 8, 26)), Movie("2", "Film", released: new(2016, 8, 26))],
            new() { Query = "Film", HintedIDs = [new(TestSources.Plugin, MetadataEntityType.Movie, "2")] });

        Assert.Equal(["2", "1"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal(MatchRejectionReason.Outranked, matches[1].Rejection);
    }

    [Fact]
    public void MatchMovies_WithoutAQuery_TakesShortTitlesOnlyExactly()
    {
        var (anime, episode) = FilmAnime(new(2016, 8, 26), 1);
        Mock.Get(anime).SetupGet(entry => entry.Titles).Returns(
        [
            Title("Kimi no Na wa.", MetadataSource.AniDB, "x-jat", "", TitleType.Main),
            Title("Kimi", MetadataSource.AniDB, "x-jat", "", TitleType.Short),
        ]);
        Assert.Equal(MatchRating.None, Assert.Single(Matcher().MatchMovies(anime, episode, [Movie("1", "Kimi to Boku", released: new(2011, 1, 1))])).Rating);
        Assert.Equal(MatchRating.DateAndTitleMatches, Assert.Single(Matcher().MatchMovies(anime, episode, [Movie("1", "Kimi no Na wa.", released: new(2016, 8, 26))])).Rating);
    }

    [Fact]
    public void MatchMovies_KeepsTheSourcesOrderForATie()
    {
        var (anime, episode) = FilmAnime(new(2016, 8, 26), 1);
        var matches = Matcher().MatchMovies(anime, episode,
        [
            Movie("1", "Film", released: new(2016, 8, 26)) with { IsStandaloneVideo = true },
            Movie("2", "Film", released: new(2016, 8, 26)),
        ], new() { Query = "Film" });

        Assert.Equal(["1", "2"], matches.Select(match => match.Candidate.ID.ID));
        Assert.Equal(MatchRejectionReason.Outranked, matches[1].Rejection);
    }

    [Fact]
    public void MatchMovies_ThrowsForAnEpisodeOfAnotherAnime()
    {
        var (anime, _) = FilmAnime(new(2016, 8, 26), 1);
        var (_, other) = FilmAnime(new(2016, 8, 26), 1, animeID: 2);
        Assert.Throws<ArgumentException>(() => Matcher().MatchMovies(anime, other, []));
    }

    #endregion

    #region Stubs

    private static IAnidbEpisode HiddenAnidbEpisode(int id, int episodeNumber)
    {
        var shoko = new Mock<IShokoEpisode>();
        shoko.SetupGet(episode => episode.IsHidden).Returns(true);

        var mock = new Mock<IAnidbEpisode>();
        mock.SetupGet(episode => episode.AnidbID).Returns(id);
        mock.SetupGet(episode => episode.ID).Returns(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, id.ToString()));
        mock.SetupGet(episode => episode.Source).Returns(MetadataSource.AniDB);
        mock.SetupGet(episode => episode.EntityType).Returns(MetadataEntityType.Episode);
        mock.SetupGet(episode => episode.Type).Returns(EpisodeType.Special);
        mock.SetupGet(episode => episode.EpisodeNumber).Returns(episodeNumber);
        mock.SetupGet(episode => episode.SeasonNumber).Returns((int?)null);
        mock.SetupGet(episode => episode.AirDate).Returns((DateOnly?)null);
        mock.SetupGet(episode => episode.RegularAirDate).Returns((DateOnly?)null);
        mock.SetupGet(episode => episode.Titles).Returns([]);
        mock.SetupGet(episode => episode.ShokoEpisodes).Returns([shoko.Object]);
        return mock.Object;
    }

    // Matches what ProviderEpisode advertises, so the engine can find the
    // candidate the link names. A null ID is a link to nothing.
    private static IMetadataEpisodeCrossReference CrossReference(int anidbEpisodeID, string? providerID, MatchRating rating)
    {
        var mock = new Mock<IMetadataEpisodeCrossReference>();
        mock.SetupGet(xref => xref.AnidbEpisodeID).Returns(anidbEpisodeID);
        mock.SetupGet(xref => xref.Source).Returns(TestSources.Plugin);
        mock.SetupGet(xref => xref.ProviderID).Returns(providerID is null ? null : new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, providerID));
        mock.SetupGet(xref => xref.MatchRating).Returns(rating);
        mock.SetupGet(xref => xref.Ordering).Returns(0);
        return mock.Object;
    }

    private static IAnidbAnime SeriesAnime(DateOnly airDate, int episodeCount, DateOnly? regularAirDate = null, IReadOnlyList<string>? titles = null, DateOnly? endDate = null)
    {
        var episodes = Enumerable.Range(1, episodeCount)
            .Select(number => AnidbEpisode(number, number, airDate.AddDays(7 * (number - 1)),
                regularAirDate: (regularAirDate ?? airDate).AddDays(7 * (number - 1))))
            .ToList();
        var mock = new Mock<IAnidbAnime>();
        mock.SetupGet(anime => anime.AnidbID).Returns(1);
        mock.SetupGet(anime => anime.Type).Returns(AnimeType.TV);
        mock.SetupGet(anime => anime.AirDate).Returns(new PartialDateOnly(airDate));
        mock.SetupGet(anime => anime.RegularAirDate).Returns(new PartialDateOnly(regularAirDate ?? airDate));
        mock.SetupGet(anime => anime.EndDate).Returns(endDate is { } ended ? new PartialDateOnly(ended) : null);
        mock.SetupGet(anime => anime.Episodes).Returns(episodes);
        mock.SetupGet(anime => anime.EpisodeCounts).Returns(new EpisodeCounts { Episodes = episodeCount });
        mock.SetupGet(anime => anime.Titles).Returns([.. (titles ?? []).Select(title => Title(title, MetadataSource.AniDB, "x-jat"))]);
        return mock.Object;
    }

    private static (IAnidbAnime Anime, IAnidbEpisode Episode) FilmAnime(DateOnly airDate, int films, DateOnly? episodeDate = null, int animeID = 1)
    {
        var episodes = Enumerable.Range(1, films)
            .Select(number => FilmEpisode(animeID * 100 + number, animeID, number, number == films && episodeDate is not null ? episodeDate : airDate))
            .ToList();
        var mock = new Mock<IAnidbAnime>();
        mock.SetupGet(anime => anime.AnidbID).Returns(animeID);
        mock.SetupGet(anime => anime.Type).Returns(AnimeType.Movie);
        mock.SetupGet(anime => anime.AirDate).Returns(new PartialDateOnly(airDate));
        mock.SetupGet(anime => anime.RegularAirDate).Returns(new PartialDateOnly(airDate));
        mock.SetupGet(anime => anime.Episodes).Returns(episodes);
        mock.SetupGet(anime => anime.Titles).Returns([]);
        return (mock.Object, episodes[^1]);
    }

    private static IAnidbEpisode FilmEpisode(int id, int animeID, int number, DateOnly? airDate)
    {
        var mock = new Mock<IAnidbEpisode>();
        mock.SetupGet(episode => episode.AnidbID).Returns(id);
        mock.SetupGet(episode => episode.AnidbAnimeID).Returns(animeID);
        mock.SetupGet(episode => episode.Type).Returns(EpisodeType.Episode);
        mock.SetupGet(episode => episode.EpisodeNumber).Returns(number);
        mock.SetupGet(episode => episode.AirDate).Returns(airDate);
        mock.SetupGet(episode => episode.RegularAirDate).Returns(airDate);
        mock.SetupGet(episode => episode.Titles).Returns([Title($"Part {number}", MetadataSource.AniDB)]);
        return mock.Object;
    }

    private static MetadataSeriesSearchResult Series(string id, string title, DateOnly? aired = null, IReadOnlyList<string>? alternateTitles = null, IReadOnlyList<MetadataSearchResultSeason>? seasons = null)
        => new()
        {
            ID = new(TestSources.Plugin, MetadataEntityType.Series, id),
            Title = title,
            AlternateTitles = alternateTitles ?? [],
            FirstAiredAt = aired is { } date ? new PartialDateOnly(date) : null,
            Seasons = seasons,
        };

    private static MetadataSearchResultSeason Season(int number, int episodeCount, DateOnly aired)
        => new() { SeasonNumber = number, EpisodeCount = episodeCount, FirstAiredAt = new(aired) };

    private static MetadataSearchResultEpisode Episode(int number, DateOnly? aired)
        => new() { EpisodeNumber = number, AiredAt = aired };

    // One episode a week from the first, numbered on from the given number.
    private static IReadOnlyList<MetadataSearchResultEpisode> Weekly(DateOnly first, int fromNumber, int count)
        => [.. Enumerable.Range(0, count).Select(index => Episode(fromNumber + index, first.AddDays(7 * index)))];

    // An anime whose regular episodes aired on the days given.
    private static IAnidbAnime DatedAnime(IReadOnlyList<(int Number, DateOnly Date)> dates)
    {
        var episodes = dates.Select(pair => AnidbEpisode(pair.Number, pair.Number, pair.Date)).ToList();
        var mock = new Mock<IAnidbAnime>();
        mock.SetupGet(anime => anime.AnidbID).Returns(1);
        mock.SetupGet(anime => anime.Type).Returns(AnimeType.TV);
        mock.SetupGet(anime => anime.AirDate).Returns(new PartialDateOnly(dates[0].Date));
        mock.SetupGet(anime => anime.RegularAirDate).Returns(new PartialDateOnly(dates[0].Date));
        mock.SetupGet(anime => anime.Episodes).Returns(episodes);
        mock.SetupGet(anime => anime.EpisodeCounts).Returns(new EpisodeCounts { Episodes = dates.Count });
        mock.SetupGet(anime => anime.Titles).Returns([]);
        return mock.Object;
    }

    private static MetadataMovieSearchResult Movie(string id, string title, DateOnly? released = null, IReadOnlyList<DateOnly>? otherReleases = null)
        => new()
        {
            ID = new(TestSources.Plugin, MetadataEntityType.Movie, id),
            Title = title,
            ReleasedAt = released is { } date ? new PartialDateOnly(date) : null,
            OtherReleaseDates = otherReleases ?? [],
        };

    private static IAnidbAnime Anime(AnimeType type)
    {
        var mock = new Mock<IAnidbAnime>();
        mock.SetupGet(anime => anime.Type).Returns(type);
        mock.SetupGet(anime => anime.Titles).Returns([]);
        return mock.Object;
    }

    private static IAnidbEpisode AnidbEpisode(
        int id,
        int episodeNumber,
        DateOnly? airDate,
        EpisodeType type = EpisodeType.Episode,
        string? title = null,
        IReadOnlyList<ITitle>? titles = null,
        IAnidbAnime? series = null,
        DateOnly? regularAirDate = null
    )
    {
        var mock = new Mock<IAnidbEpisode>();
        mock.SetupGet(episode => episode.AnidbID).Returns(id);
        mock.SetupGet(episode => episode.ID).Returns(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, id.ToString()));
        mock.SetupGet(episode => episode.Source).Returns(MetadataSource.AniDB);
        mock.SetupGet(episode => episode.EntityType).Returns(MetadataEntityType.Episode);
        mock.SetupGet(episode => episode.Type).Returns(type);
        mock.SetupGet(episode => episode.EpisodeNumber).Returns(episodeNumber);
        mock.SetupGet(episode => episode.SeasonNumber).Returns((int?)null);
        mock.SetupGet(episode => episode.AirDate).Returns(airDate);
        mock.SetupGet(episode => episode.RegularAirDate).Returns(regularAirDate ?? airDate);
        mock.SetupGet(episode => episode.Titles).Returns(titles ?? (title is null ? [] : [Title(title, MetadataSource.AniDB)]));
        mock.SetupGet(episode => episode.ShokoEpisodes).Returns([]);
        mock.SetupGet(episode => episode.MetadataEpisodeCrossReferences).Returns([]);
        if (series is not null)
            mock.SetupGet(episode => episode.Series).Returns(series);
        return mock.Object;
    }

    private static IEpisode ProviderEpisode(
        int id,
        int episodeNumber,
        int? seasonNumber,
        DateOnly? airDate,
        string? title = null,
        IReadOnlyList<ITitle>? titles = null,
        string? originalLanguage = null
    )
    {
        var mock = new Mock<IEpisode>();
        mock.SetupGet(episode => episode.ID).Returns(new MetadataGuid(TestSources.Plugin, MetadataEntityType.Episode, id.ToString()));
        mock.SetupGet(episode => episode.Source).Returns(TestSources.Plugin);
        mock.SetupGet(episode => episode.EntityType).Returns(MetadataEntityType.Episode);
        mock.SetupGet(episode => episode.Type).Returns(EpisodeType.Episode);
        mock.SetupGet(episode => episode.EpisodeNumber).Returns(episodeNumber);
        mock.SetupGet(episode => episode.SeasonNumber).Returns(seasonNumber);
        mock.SetupGet(episode => episode.AirDate).Returns(airDate);
        mock.SetupGet(episode => episode.Titles).Returns(titles ?? (title is null ? [] : [Title(title, TestSources.Plugin)]));
        if (originalLanguage is not null)
        {
            var series = new Mock<ISeries>();
            series.SetupGet(show => show.OriginalLanguageCode).Returns(originalLanguage);
            mock.SetupGet(episode => episode.Series).Returns(series.Object);
        }

        return mock.Object;
    }

    private static ITitle Title(string value, MetadataSource source, string languageCode = "en", string countryCode = "US", TitleType type = TitleType.Official)
    {
        var mock = new Mock<ITitle>();
        mock.SetupGet(title => title.Value).Returns(value);
        mock.SetupGet(title => title.Language).Returns(languageCode.GetTitleLanguage());
        mock.SetupGet(title => title.LanguageCode).Returns(languageCode);
        mock.SetupGet(title => title.CountryCode).Returns(countryCode);
        mock.SetupGet(title => title.Type).Returns(type);
        mock.SetupGet(title => title.Source).Returns(source);
        return mock.Object;
    }

    #endregion
}
