using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.HTTP.GetAnime;
using Xunit;

namespace Shoko.Tests.Providers.AniDB;

/// <summary>
/// Covers when AniDB anime and episodes are stamped as first stored: once on
/// insert, and for the rows stored before, from the Shoko entry, the cached
/// anime XML's write time, or the anime's own date, in that order.
/// </summary>
public class AnidbCreationDateTests
{
    #region Insert

    [Fact]
    public void ANewAnimeIsStampedOnceAndAnUpdateKeepsTheStamp()
    {
        var anime = new AniDB_Anime();
        var before = DateTime.Now;

        AnimeCreator.PopulateAnime(Response(), anime);
        var createdAt = anime.CreatedAt;
        Assert.InRange(createdAt, before, DateTime.Now);

        // Saved, the row has its local ID, and the next update is no insert.
        anime.AniDB_AnimeID = 7;
        AnimeCreator.PopulateAnime(Response(voteCount: 42), anime);

        Assert.Equal(createdAt, anime.CreatedAt);
        Assert.Equal(42, anime.VoteCount);
    }

    [Fact]
    public void ANewEpisodeIsStampedWithWhenItIsFirstStored()
    {
        var createdAt = new DateTime(2026, 5, 6, 7, 8, 9);
        var lastUpdated = new DateTime(2024, 1, 2);

        var episode = AnimeCreator.NewEpisode(new ResponseEpisode { EpisodeID = 2, AnimeID = 1, Description = "", LastUpdated = lastUpdated }, createdAt);

        Assert.Equal((createdAt, lastUpdated), (episode.CreatedAt, episode.DateTimeUpdated));
    }

    #endregion

    #region Backfill

    private static readonly DateTime _unset = new(1970, 1, 1);

    private static readonly DateTime _seriesCreatedAt = new(2021, 1, 1);

    private static readonly DateTime _shokoEpisodeCreatedAt = new(2021, 2, 2);

    private static readonly DateTime _xmlWrittenAt = new(2022, 3, 3);

    private static readonly DateTime _animeUpdatedAt = new(2023, 4, 4);

    private static readonly DateTime _episodeUpdatedAt = new(2019, 5, 5);

    [Fact]
    public void AnAnimeTakesItsSeriesDateThenItsXmlTimeThenItsLastUpdate()
    {
        var withSeries = Anime(1);
        var withXml = Anime(2);
        var withNeither = Anime(3);
        var plan = Plan([withSeries, withXml, withNeither], [], seriesDates: new() { [1] = _seriesCreatedAt }, xml: new() { [1] = _xmlWrittenAt, [2] = _xmlWrittenAt });

        Assert.Equal([(withSeries, _seriesCreatedAt), (withXml, _xmlWrittenAt), (withNeither, _animeUpdatedAt)], plan.Anime);
    }

    [Fact]
    public void AnEpisodeTakesItsShokoDateThenItsAnimeXmlTimeThenItsAnimeDate()
    {
        var stamped = Anime(3);
        stamped.CreatedAt = new DateTime(2025, 6, 6);
        var withShoko = Episode(10, animeID: 1);
        var withXml = Episode(11, animeID: 1);
        var ofAnimeDatedNow = Episode(12, animeID: 2);
        var ofStampedAnime = Episode(13, animeID: 3);
        var ofUpdatedAnime = Episode(14, animeID: 4);
        var plan = Plan(
            [Anime(1), Anime(2), stamped, Anime(4)],
            [withShoko, withXml, ofAnimeDatedNow, ofStampedAnime, ofUpdatedAnime],
            seriesDates: new() { [2] = _seriesCreatedAt },
            episodeDates: new() { [10] = _shokoEpisodeCreatedAt },
            xml: new() { [1] = _xmlWrittenAt }
        );

        // The episode's own update is AniDB's, so it is never used.
        Assert.Equal(
            [
                (withShoko, _shokoEpisodeCreatedAt),
                (withXml, _xmlWrittenAt),
                (ofAnimeDatedNow, _seriesCreatedAt),
                (ofStampedAnime, stamped.CreatedAt),
                (ofUpdatedAnime, _animeUpdatedAt),
            ],
            plan.Episodes
        );
    }

    [Fact]
    public void ARowWithACreationDateOrNothingKnownIsLeftAlone()
    {
        var stamped = Anime(1);
        stamped.CreatedAt = new DateTime(2025, 6, 6);
        var stampedEpisode = Episode(10, animeID: 1);
        stampedEpisode.CreatedAt = stamped.CreatedAt;
        var orphan = Episode(11, animeID: 99);

        var plan = Plan([stamped], [stampedEpisode, orphan], seriesDates: new() { [1] = _seriesCreatedAt });

        Assert.Empty(plan.Anime);
        Assert.Empty(plan.Episodes);
    }

    [Fact]
    public void TheXmlWriteTimesAreReadByAnimeIDFromTheCacheFolder()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"shoko-anime-xml-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "AnimeDoc_123.xml");
            File.WriteAllText(path, "<anime />");
            File.SetLastWriteTime(path, _xmlWrittenAt);
            File.WriteAllText(Path.Combine(directory, "AnimeDoc_abc.xml"), "<anime />");
            File.WriteAllText(Path.Combine(directory, "Other_456.xml"), "<anime />");

            var writeTimes = DatabaseFixes.ReadAnimeXmlWriteTimes(directory);

            Assert.Equal(new Dictionary<int, DateTime> { [123] = _xmlWrittenAt }, writeTimes);
            Assert.Empty(DatabaseFixes.ReadAnimeXmlWriteTimes(Path.Combine(directory, "missing")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static DatabaseFixes.AnidbCreationDatePlan Plan(
        IReadOnlyList<AniDB_Anime> anime,
        IReadOnlyList<AniDB_Episode> episodes,
        Dictionary<int, DateTime>? seriesDates = null,
        Dictionary<int, DateTime>? episodeDates = null,
        Dictionary<int, DateTime>? xml = null
    )
        => DatabaseFixes.PlanAnidbCreationDates(
            anime,
            episodes,
            seriesDates ?? [],
            episodeDates ?? [],
            animeID => xml is not null && xml.TryGetValue(animeID, out var writtenAt) ? writtenAt : null
        );

    #endregion

    #region Helpers

    private static ResponseAnime Response(int voteCount = 0)
        => new() { AnimeID = 1, MainTitle = "Anime", Description = "", URL = "", Picname = "", VoteCount = voteCount };

    private static AniDB_Anime Anime(int animeID)
        => new() { AniDB_AnimeID = animeID, AnimeID = animeID, CreatedAt = _unset, DateTimeDescUpdated = _animeUpdatedAt };

    private static AniDB_Episode Episode(int episodeID, int animeID)
        => new() { AniDB_EpisodeID = episodeID, EpisodeID = episodeID, AnimeID = animeID, CreatedAt = _unset, DateTimeUpdated = _episodeUpdatedAt };

    #endregion
}
