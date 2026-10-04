using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.TestData;
using Shoko.Tests.Infrastructure;
using Xunit;

using AnidbRegularAirDates = Shoko.Server.Providers.AniDB.AnidbRegularAirDates;
using Outcome = Shoko.Server.Providers.AniDB.AnidbRegularAirDates.Outcome;

namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// Covers how <see cref="AnidbRegularAirDates"/> reads an anime's
/// early-showing note and moves the early episodes onto the regular run,
/// against the prototype it was ported from.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnidbRegularAirDatesTests
{
    #region Dates

    [Theory]
    [InlineData("reened on November 8, 2020 on M", "November 8, 2020", 2020, 11, 8, "mdy")]
    [InlineData(" event on 24 September 2019 some", "24 September 2019", 2019, 9, 24, "dmy")]
    [InlineData("ngeles on 02.07.2017. The", "02.07.2017", 2017, 7, 2, "dmy_dot")]
    [InlineData("episodes 1 and 2 on 1.2.2003", "1.2.2003", 2003, 2, 1, "dmy_dot")]
    [InlineData("conico on Jan 7. The", "Jan 7", null, 1, 7, "mdy+noyear")]
    [InlineData("eatres in October 2020, bef", "October 2020", 2020, 10, null, "my")]
    [InlineData(" Douga on April 3rd prev", "April 3rd", null, 4, 3, "mdy+noyear+ordinal")]
    [InlineData("tarted on October 2nd, 2015, and", "October 2nd, 2015", 2015, 10, 2, "mdy+ordinal")]
    [InlineData("hannel on 24 March. The r", "24 March. ", null, 3, 24, "dmy+noyear")]
    [InlineData("arting on July 11 and 12, 2020, at ", "July 11 and 12, 2020", 2020, 7, 11, "range_mdy")]
    [InlineData("October 19-21, 2012", "October 19-21, 2012", 2012, 10, 19, "range_mdy")]
    [InlineData("ekijou on 6th of April, 2012. A d", "6th of April, 2012", 2012, 4, 6, "dmy+ordinal")]
    [InlineData("leased in 2001.08.08 on D", "2001.08.08", 2001, 8, 8, "iso")]
    [InlineData("s between 24 and 29 June 2019. The", "24 and 29 June 2019", 2019, 6, 24, "range_dmy")]
    [InlineData("from 19-21 October 2012", "19-21 October 2012", 2012, 10, 19, "range_dmy")]
    [InlineData("s Expo on March 30 and 31. Reg", "March 30 and 31", null, 3, 30, "range_mdy+noyear")]
    [InlineData("ednesday, 22nd December. 1000 ", "22nd December. ", null, 12, 22, "dmy+noyear+ordinal")]
    [InlineData("on Saturday, April 2, 2016 at", "April 2, 2016", 2016, 4, 2, "mdy")]
    [InlineData("on Sept. 30, 2016", "Sept. 30, 2016", 2016, 9, 30, "mdy")]
    public void FindDates_ReadsEveryFormat(string text, string matched, int? year, int month, int? day, string tag)
    {
        var date = Assert.Single(AnidbRegularAirDates.FindDates(text));
        Assert.Equal(matched, text[date.Start..date.End]);
        Assert.Equal(year, date.Year);
        Assert.Equal(month, date.Month);
        Assert.Equal(day, date.Day);
        Assert.Equal(tag, date.Tag);
    }

    [Fact]
    public void FindDates_SkipsTimesAndKeepsOrder()
    {
        var dates = AnidbRegularAirDates.FindDates("starting July 6 18:30 PDT (July 7 10:30 JST)");
        Assert.Equal([(7, 6), (7, 7)], dates.Select(date => (date.Month, date.Day!.Value)));
        Assert.All(dates, date => Assert.Null(date.Year));
    }

    [Fact]
    public void FindDates_DropsImpossibleDays()
        => Assert.Empty(AnidbRegularAirDates.FindDates("on 45.13.2020"));

    [Theory]
    [InlineData(1, 7, "2019-12-15", true, "2020-01-07")]
    [InlineData(12, 14, "2019-12-15", true, "2019-12-14")]
    [InlineData(12, 13, "2019-12-15", true, "2020-12-13")]
    [InlineData(12, 20, "2020-01-05", false, "2019-12-20")]
    [InlineData(1, 5, "2020-01-05", false, "2020-01-05")]
    public void InferYear_TakesTheNearestOccurrence(int month, int day, string anchor, bool forward, string expected)
    {
        var date = Assert.Single(AnidbRegularAirDates.FindDates($"on {new DateOnly(2000, month, day):MMMM} {day}."));
        AnidbRegularAirDates.InferYear(date, DateOnly.Parse(anchor), forward);
        Assert.Equal(DateOnly.Parse(expected), date.Value);
    }

    [Fact]
    public void InferYear_KeepsAStatedYear()
    {
        var date = Assert.Single(AnidbRegularAirDates.FindDates("on January 7, 2016."));
        AnidbRegularAirDates.InferYear(date, new DateOnly(2020, 1, 1), true);
        Assert.Equal(new DateOnly(2016, 1, 7), date.Value);
    }

    #endregion

    #region Note

    [Fact]
    public void ReadNote_SkipsAHomeVideoStart()
    {
        var note = AnidbRegularAirDates.ReadNote("Note: The first episode was screened at an event. The DVD release started on May 5, 2016.");
        Assert.Single(note.Lines);
        Assert.Null(note.Regular);
    }

    [Theory]
    [InlineData("The first three episodes were screened early.", 3)]
    [InlineData("The first 12 episodes were streamed early.", 12)]
    [InlineData("The first episode was pre-aired.", 1)]
    [InlineData("Episodes 1-5 received a pre-airing.", 5)]
    [InlineData("The first and second episodes were pre-aired.", 2)]
    [InlineData("Both episodes were screened early.", 2)]
    [InlineData("The show premiered on TV.", null)]
    public void StatedCount_ReadsTheLeadingCount(string line, int? expected)
        => Assert.Equal(expected, AnidbRegularAirDates.StatedCount(line));

    [Fact]
    public void ReadNote_MarksEveryEpisodeAhead()
        => Assert.True(AnidbRegularAirDates.ReadNote("Note: Each episode was streamed a week ahead of the TV broadcast.").Each);

    #endregion

    #region Rules

    private static List<(int, DateOnly)> Weekly(DateOnly first, int count, int from = 1)
        => [.. Enumerable.Range(0, count).Select(index => (from + index, first.AddDays(7 * index)))];

    private static List<(int, DateOnly)> Daily(DateOnly first, int count, int from = 1)
        => [.. Enumerable.Range(0, count).Select(index => (from + index, first.AddDays(index)))];

    private static void AssertMoved(AnidbRegularAirDates.Reading reading, params (int Number, string Regular)[] expected)
        => Assert.Equal(expected.Select(e => (e.Number, DateOnly.Parse(e.Regular))), reading.Episodes.Select(e => (e.EpisodeNumber, e.Regular)));

    [Fact]
    public void Rule1_PacesTheEarlyEpisodeToTheRegularStart()
    {
        List<(int, DateOnly)> episodes = [(1, new(2016, 3, 20)), .. Weekly(new(2016, 4, 14), 11, 2)];
        var reading = AnidbRegularAirDates.Read(
            "Note: The first episode received an advance screening on March 20, 2016. The regular TV broadcast started on April 7, 2016.",
            AnimeType.TVSeries,
            episodes
        );
        Assert.Equal(Outcome.Corrected, reading.Outcome);
        Assert.Equal(new DateOnly(2016, 4, 7), reading.RegularStart);
        AssertMoved(reading, (1, "2016-04-07"));
    }

    [Fact]
    public void Rule1_CapsAtThreeWithoutACount()
    {
        List<(int, DateOnly)> episodes = [.. Enumerable.Range(0, 4).Select(index => (index + 1, new DateOnly(2012, 3, 1))), .. Weekly(new(2012, 5, 1), 8, 5)];
        var reading = AnidbRegularAirDates.Read("Note: A pre-airing took place. Regular broadcast started on 03.04.2012.", AnimeType.TVSeries, episodes);
        Assert.Equal(Outcome.TooManyEarly, reading.Outcome);
    }

    [Fact]
    public void Rule1_SkipsWhenEveryEpisodeIsBeforeTheStart()
    {
        var reading = AnidbRegularAirDates.Read("Note: The regular broadcast started on 03.04.2012.", AnimeType.TVSeries, Weekly(new(2012, 1, 1), 3));
        Assert.Equal(Outcome.TooManyEarly, reading.Outcome);
    }

    [Fact]
    public void Rule1_SkipsWhenEachEpisodeCameOutAhead()
    {
        var reading = AnidbRegularAirDates.Read(
            "Note: Each episode was streamed a week ahead. The regular TV broadcast started on April 7, 2016.",
            AnimeType.TVSeries,
            [(1, new(2016, 3, 31)), .. Weekly(new(2016, 4, 7), 11, 2)]
        );
        Assert.Equal(Outcome.TooManyEarly, reading.Outcome);
    }

    [Fact]
    public void Rule1_SkipsWhenTheLaterEpisodesAreEarlyToo()
    {
        var reading = AnidbRegularAirDates.Read(
            "Note: The first episode was streamed early. The regular TV broadcast started on April 7, 2016.",
            AnimeType.TVSeries,
            [(1, new(2016, 3, 31)), .. Weekly(new(2016, 4, 7), 11, 2)]
        );
        Assert.Equal(Outcome.LaterEpisodesEarlyToo, reading.Outcome);
    }

    [Fact]
    public void Rule1_PacesByTheRunsCadence()
    {
        List<(int, DateOnly)> episodes = [(1, new(2020, 1, 1)), (2, new(2020, 1, 1)), .. Daily(new(2020, 2, 4), 8, 3)];
        var reading = AnidbRegularAirDates.Read(
            "Note: The first two episodes were streamed early. The regular TV broadcast started on February 1, 2020.",
            AnimeType.TVSeries,
            episodes
        );
        AssertMoved(reading, (1, "2020-02-01"), (2, "2020-02-02"));
    }

    [Fact]
    public void Rule1_InfersAMissingYearFromTheFirstEpisode()
    {
        var reading = AnidbRegularAirDates.Read(
            "Note: The first episode was pre-aired on December 20. The regular TV broadcast started on January 7.",
            AnimeType.TVSeries,
            [(1, new(2015, 12, 20)), .. Weekly(new(2016, 1, 14), 11, 2)]
        );
        Assert.Equal(new DateOnly(2016, 1, 7), reading.RegularStart);
        AssertMoved(reading, (1, "2016-01-07"));
    }

    [Fact]
    public void Rule1b_CountsTheStartFromTheEarlyShowing()
    {
        var reading = AnidbRegularAirDates.Read(
            "Note: The first 2 episodes along with the last episode of the previous season were shown at a special event on 06.04.2012, " +
            "2 days before the TV broadcast.",
            AnimeType.TVSeries,
            [(1, new(2012, 4, 6)), (2, new(2012, 4, 6)), .. Weekly(new(2012, 4, 22), 11, 3)]
        );
        Assert.Equal(new DateOnly(2012, 4, 8), reading.RegularStart);
        AssertMoved(reading, (1, "2012-04-08"), (2, "2012-04-15"));
    }

    // Spider Riders (4242): no regular start named, three counted early
    // episodes set apart from the weekly run by a month.
    [Fact]
    public void Rule2_BackPacesTheCountedEpisodes()
    {
        List<(int, DateOnly)> episodes = [(1, new(2006, 3, 25)), (2, new(2006, 3, 25)), (3, new(2006, 3, 25)), .. Weekly(new(2006, 4, 26), 23, 4)];
        const string Note = "Note: The first three episodes premiered in North America with a 2 month hiatus between episodes 3 and 4, " +
            "after which the series continued without a break between seasons. Episodes 4-26 aired first in Japan.";
        var reading = AnidbRegularAirDates.Read(Note, AnimeType.TVSeries, episodes);
        Assert.Equal(Outcome.CorrectedFromCount, reading.Outcome);
        AssertMoved(reading, (1, "2006-04-05"), (2, "2006-04-12"), (3, "2006-04-19"));

        Assert.Equal(Outcome.NoRegularDate, AnidbRegularAirDates.Read(Note, AnimeType.OVA, episodes).Outcome);
    }

    [Fact]
    public void NoNote_MovesNothing()
    {
        var reading = AnidbRegularAirDates.Read("A story about a girl.", AnimeType.TVSeries, Weekly(new(2016, 4, 7), 12));
        Assert.Equal(Outcome.NoNote, reading.Outcome);
        Assert.Empty(reading.Episodes);
    }

    #endregion

    #region Prototype parity

    private static readonly Dictionary<string, Outcome> _statuses = new()
    {
        ["none"] = Outcome.NoNote,
        ["no-regular-date"] = Outcome.NoRegularDate,
        ["regular-unparsed"] = Outcome.RegularDateUnreadable,
        ["no-dated-episodes"] = Outcome.NoDatedEpisodes,
        ["no-early-episodes"] = Outcome.NoEarlyEpisodes,
        ["too-many-before"] = Outcome.TooManyEarly,
        ["later-episodes-early-too"] = Outcome.LaterEpisodesEarlyToo,
        ["corrected"] = Outcome.Corrected,
        ["corrected-rule2"] = Outcome.CorrectedFromCount,
    };

    private static AnidbRegularAirDates.Reading Read(RegularAirDateCase testCase)
        => AnidbRegularAirDates.Read(testCase.Description, (AnimeType)testCase.Type, testCase.TypedEpisodes, testCase.CompleteAirDate);

    // Every anime of the prototype's run with an early-showing note, its
    // result exported as the fixture: the port has to agree on each.
    [Fact]
    public void EveryNotedAnime_MatchesThePrototype()
    {
        var cases = TestData.TestData.AnidbRegularAirDates.Value;
        Assert.NotEmpty(cases);

        var mismatches = new List<string>();
        foreach (var testCase in cases)
        {
            var reading = Read(testCase);
            var expected = testCase.TypedMoved.ToList();
            var actual = reading.Episodes.Select(episode => (episode.EpisodeNumber, episode.Stored, episode.Regular)).ToList();
            if (reading.Outcome != _statuses[testCase.Status] || reading.RegularStart != testCase.RegularStart || !expected.SequenceEqual(actual))
                mismatches.Add($"{testCase.AnimeID}: expected {testCase.Status} {testCase.RegularStart} [{string.Join(", ", expected)}], " +
                    $"got {reading.Outcome} {reading.RegularStart} [{string.Join(", ", actual)}]");
        }

        Assert.Empty(mismatches);
    }

    #endregion

    #region Model

    private const int AnimeID = 14987;

    private static AniDB_Episode Episode(int number, DateOnly airDate, EpisodeType type = EpisodeType.Episode) => new()
    {
        AniDB_EpisodeID = number + (type is EpisodeType.Episode ? 0 : 100),
        EpisodeID = 1000 + number + (type is EpisodeType.Episode ? 0 : 100),
        AnimeID = AnimeID,
        EpisodeType = type,
        EpisodeNumber = number,
        AirDate = (int)(airDate.ToDateTime(TimeOnly.MinValue) - DateTime.UnixEpoch).TotalSeconds,
    };

    private static (AniDB_Anime Anime, List<AniDB_Episode> Episodes, RepoFactoryScope Scope) IdInvaded()
    {
        var anime = new AniDB_Anime
        {
            AniDB_AnimeID = 1,
            AnimeID = AnimeID,
            AnimeType = AnimeType.TVSeries,
            AirDate = new PartialDateOnly(2019, 12, 15),
            Description = "[i]Note: The first two episodes were streamed by Funimation and Wakanim on Dec 15 for 24 hours. " +
                "The regular TV broadcast started on Jan 5, 2020.[/i]",
        };
        List<AniDB_Episode> episodes =
        [
            Episode(1, new(2019, 12, 15)),
            Episode(2, new(2019, 12, 15)),
            .. Enumerable.Range(0, 11).Select(index => Episode(index + 3, new DateOnly(2020, 1, 13).AddDays(7 * index))),
            Episode(1, new(2019, 12, 1), EpisodeType.Special),
        ];
        var scope = new RepoFactoryScope()
            .With<AniDB_AnimeRepository, int, AniDB_Anime>(a => a.AniDB_AnimeID, [anime])
            .With<AniDB_EpisodeRepository, int, AniDB_Episode>(e => e.AniDB_EpisodeID, episodes);
        return (anime, episodes, scope);
    }

    [Fact]
    public void Episode_ShownEarly_IsDatedByItsRegularBroadcast()
    {
        var (_, episodes, scope) = IdInvaded();
        using (scope)
        {
            var first = (IEpisode)episodes[0];
            Assert.Equal(new DateOnly(2020, 1, 5), first.AirDate);
            Assert.Equal(new DateTime(2020, 1, 5), first.AirDateWithTime);
            Assert.Equal(new DateOnly(2019, 12, 15), first.EarlyAirDate);
            Assert.Equal(new DateOnly(2020, 1, 5), ((IEpisode)episodes[1]).AirDate);
            Assert.Equal(new DateOnly(2019, 12, 15), ((IEpisode)episodes[1]).EarlyAirDate);

            // The stored column keeps AniDB's own date.
            Assert.Equal(new DateOnly(2019, 12, 15), episodes[0].GetAirDateAsDateOnly());
        }
    }

    [Fact]
    public void Episode_NotShownEarly_HasNoEarlyAirDate()
    {
        var (_, episodes, scope) = IdInvaded();
        using (scope)
        {
            var third = (IEpisode)episodes[2];
            Assert.Equal(new DateOnly(2020, 1, 13), third.AirDate);
            Assert.Null(third.EarlyAirDate);

            var special = (IEpisode)episodes[^1];
            Assert.Equal(new DateOnly(2019, 12, 1), special.AirDate);
            Assert.Null(special.EarlyAirDate);
        }
    }

    [Fact]
    public void Episode_WithoutANote_KeepsAniDBsDate()
    {
        var (anime, episodes, scope) = IdInvaded();
        using (scope)
        {
            anime.Description = string.Empty;
            anime.ResetRegularAirDates();

            var first = (IEpisode)episodes[0];
            Assert.Equal(new DateOnly(2019, 12, 15), first.AirDate);
            Assert.Equal(new DateTime(2019, 12, 15), first.AirDateWithTime);
            Assert.Null(first.EarlyAirDate);
        }
    }

    [Fact]
    public void ShokoEpisode_TakesItsAnidbEpisodesDates()
    {
        var (_, episodes, scope) = IdInvaded();
        using (scope)
        {
            var shokoEpisode = (IEpisode)new AnimeEpisode { AniDB_EpisodeID = episodes[0].EpisodeID };
            Assert.Equal(new DateOnly(2020, 1, 5), shokoEpisode.AirDate);
            Assert.Equal(new DateOnly(2019, 12, 15), shokoEpisode.EarlyAirDate);
        }
    }

    [Fact]
    public void Anime_StartsWithTheFirstEpisodesRegularDate()
    {
        var (anime, _, scope) = IdInvaded();
        using (scope)
        {
            var anidbAnime = (IAnidbAnime)anime;
            Assert.Equal(new PartialDateOnly(2020, 1, 5), AnidbRegularAirDates.RegularStartOf(anidbAnime.AirDate, anidbAnime.Episodes));

            // The anime's own date stays AniDB's.
            Assert.Equal(new PartialDateOnly(2019, 12, 15), anidbAnime.AirDate);
        }
    }

    [Fact]
    public void Anime_KeepsItsReadingUntilItIsImportedAgain()
    {
        var (anime, episodes, scope) = IdInvaded();
        using (scope)
        {
            Assert.Equal(new DateOnly(2020, 1, 5), ((IEpisode)episodes[0]).AirDate);

            anime.Description = string.Empty;
            Assert.Equal(new DateOnly(2020, 1, 5), ((IEpisode)episodes[0]).AirDate);

            anime.ResetRegularAirDates();
            Assert.Equal(new DateOnly(2019, 12, 15), ((IEpisode)episodes[0]).AirDate);
            Assert.Null(((IEpisode)episodes[0]).EarlyAirDate);
        }
    }

    // The calendar moves an upcoming anime's date without importing it again,
    // so the note is read again at once.
    [Fact]
    public void Anime_ReadsAgainOnANewAirDate_WithoutAnImport()
    {
        var (anime, episodes, scope) = IdInvaded();
        using (scope)
        {
            Assert.Equal(new DateOnly(2020, 1, 5), ((IEpisode)episodes[0]).AirDate);

            anime.Description = string.Empty;
            anime.AirDate = new PartialDateOnly(2020, 4, 5);
            Assert.Equal(new DateOnly(2019, 12, 15), ((IEpisode)episodes[0]).AirDate);
            Assert.Null(((IEpisode)episodes[0]).EarlyAirDate);
        }
    }

    [Fact]
    public void Episode_WhoseStoredDateChanged_KeepsIt()
    {
        var (_, episodes, scope) = IdInvaded();
        using (scope)
        {
            Assert.Equal(new DateOnly(2020, 1, 5), ((IEpisode)episodes[0]).AirDate);
            episodes[0].AirDate = (int)(new DateTime(2020, 1, 4) - DateTime.UnixEpoch).TotalSeconds;
            Assert.Equal(new DateOnly(2020, 1, 4), ((IEpisode)episodes[0]).AirDate);
            Assert.Null(((IEpisode)episodes[0]).EarlyAirDate);
        }
    }

    #endregion

    #region Regular Start

    private static IEpisode DatedEpisode(int number, DateOnly airDate, DateOnly? earlyAirDate = null, EpisodeType type = EpisodeType.Episode)
    {
        var mock = new Mock<IEpisode>();
        mock.SetupGet(episode => episode.Type).Returns(type);
        mock.SetupGet(episode => episode.EpisodeNumber).Returns(number);
        mock.SetupGet(episode => episode.AirDate).Returns(airDate);
        mock.SetupGet(episode => episode.EarlyAirDate).Returns(earlyAirDate);
        return mock.Object;
    }

    [Fact]
    public void RegularStart_OfAnEarlyFirstEpisode_IsItsRegularDate()
        => Assert.Equal(
            new PartialDateOnly(2020, 1, 5),
            AnidbRegularAirDates.RegularStartOf(
                new PartialDateOnly(2019, 12, 15),
                [DatedEpisode(1, new(2020, 1, 5), new(2019, 12, 15)), DatedEpisode(2, new(2020, 1, 12))]
            )
        );

    // Only the first normal episode counts, not a special numbered one.
    [Fact]
    public void RegularStart_WithoutAnEarlyFirstEpisode_IsTheAnimesDate()
        => Assert.Equal(
            new PartialDateOnly(2016, 4, 1),
            AnidbRegularAirDates.RegularStartOf(
                new PartialDateOnly(2016, 4, 1),
                [
                    DatedEpisode(1, new(2016, 4, 7), new(2016, 3, 1), EpisodeType.Special),
                    DatedEpisode(1, new(2016, 4, 7)),
                    DatedEpisode(2, new(2016, 4, 14), new(2016, 3, 1)),
                ]
            )
        );

    [Fact]
    public void RegularStart_WithoutEpisodes_IsTheAnimesDate()
    {
        Assert.Equal(new PartialDateOnly(2016, 4, 7), AnidbRegularAirDates.RegularStartOf(new PartialDateOnly(2016, 4, 7), []));
        Assert.Null(AnidbRegularAirDates.RegularStartOf(null, []));
    }

    [Fact]
    public void RegularStart_OfAPartiallyDatedAnime_StaysPartial()
    {
        var start = AnidbRegularAirDates.RegularStartOf(new PartialDateOnly(2020, 1), [DatedEpisode(1, new(2020, 1, 5))]);
        Assert.Equal(new PartialDateOnly(2020, 1), start);
        Assert.False(start!.Value.IsComplete);

        // An early first episode still dates it to the day.
        Assert.Equal(
            new PartialDateOnly(2020, 1, 5),
            AnidbRegularAirDates.RegularStartOf(new PartialDateOnly(2020, 1), [DatedEpisode(1, new(2020, 1, 5), new(2019, 12, 15))])
        );
    }

    #endregion
}
