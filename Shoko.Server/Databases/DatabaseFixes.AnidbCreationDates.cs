using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using Shoko.Abstractions.Core.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Services;

#pragma warning disable CS0618
namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region AniDB Creation Dates | Types

    /// <summary>
    ///   The creation dates to give the AniDB anime and episodes stored
    ///   before the date was kept.
    /// </summary>
    /// <param name="Anime">Each anime with its date.</param>
    /// <param name="Episodes">Each episode with its date.</param>
    internal sealed record AnidbCreationDatePlan(
        IReadOnlyList<(AniDB_Anime Anime, DateTime CreatedAt)> Anime,
        IReadOnlyList<(AniDB_Episode Episode, DateTime CreatedAt)> Episodes
    );

    #endregion

    #region AniDB Creation Dates | Steps

    /// <summary>
    ///   Fills the creation date of every AniDB anime and episode stored
    ///   before it was kept, from the Shoko entry, else the cached anime
    ///   XML's write time, else the anime's date. Rows with a date are kept.
    /// </summary>
    public static void BackfillAnidbCreationDates()
    {
        var services = ISystemService.StaticServices;
        var systemService = services.GetRequiredService<SystemService>();
        var xmlUtils = services.GetRequiredService<HttpXmlUtils>();
        var str = systemService.StartupMessage ?? string.Empty;
        systemService.StartupMessage = $"{str} - Filling AniDB creation dates...";

        var seriesCreatedAt = RepoFactory.AnimeSeries.GetAll()
            .GroupBy(series => series.AniDB_ID)
            .ToDictionary(group => group.Key, group => group.Min(series => series.DateTimeCreated));
        var episodeCreatedAt = RepoFactory.AnimeEpisode.GetAll()
            .GroupBy(episode => episode.AniDB_EpisodeID)
            .ToDictionary(group => group.Key, group => group.Min(episode => episode.DateTimeCreated));
        var xmlWriteTimes = ReadAnimeXmlWriteTimes(xmlUtils.AnimeXmlDirectory);
        var plan = PlanAnidbCreationDates(
            RepoFactory.AniDB_Anime.GetAll(),
            RepoFactory.AniDB_Episode.GetAll(),
            seriesCreatedAt,
            episodeCreatedAt,
            animeID => xmlWriteTimes.TryGetValue(animeID, out var writtenAt) ? writtenAt : null
        );
        if (plan.Anime.Count is 0 && plan.Episodes.Count is 0)
        {
            _logger.Info("Filled AniDB creation dates: every anime and episode already has one.");
            return;
        }

        var progress = new StartupProgress(ReportTo(systemService, str), "Filling AniDB creation dates", plan.Anime.Count + plan.Episodes.Count);
        var databaseFactory = services.GetRequiredService<DatabaseFactory>();
        using var session = databaseFactory.SessionFactory.OpenStatelessSession();
        WriteInTransaction(
            session,
            () => UpdateRowsByKey(
                session,
                "AniDB_Anime",
                "AniDB_AnimeID",
                NHibernateUtil.Int32,
                plan.Anime,
                row => row.Anime.AniDB_AnimeID,
                [("CreatedAt", NHibernateUtil.DateTime, row => row.CreatedAt)],
                progress.Advance
            )
        );
        foreach (var (anime, createdAt) in plan.Anime)
            anime.CreatedAt = createdAt;

        WriteInTransaction(
            session,
            () => UpdateRowsByKey(
                session,
                "AniDB_Episode",
                "AniDB_EpisodeID",
                NHibernateUtil.Int32,
                plan.Episodes,
                row => row.Episode.AniDB_EpisodeID,
                [("CreatedAt", NHibernateUtil.DateTime, row => row.CreatedAt)],
                progress.Advance
            )
        );
        foreach (var (episode, createdAt) in plan.Episodes)
            episode.CreatedAt = createdAt;

        _logger.Info($"Filled AniDB creation dates for {plan.Anime.Count} anime and {plan.Episodes.Count} episodes.");
        systemService.StartupMessage = $"{str} - Filled AniDB creation dates for {plan.Anime.Count} anime and {plan.Episodes.Count} episodes.";
    }

    /// <summary>
    ///   Works out the creation date of each AniDB anime and episode without
    ///   one: the Shoko entry's, else the anime XML's write time, else, for
    ///   an anime, its last update and, for an episode, its anime's date, as
    ///   no episode is older than its anime. A row with nothing known is left out.
    /// </summary>
    /// <param name="anime">The stored AniDB anime.</param>
    /// <param name="episodes">The stored AniDB episodes.</param>
    /// <param name="seriesCreatedAt">When each Shoko series was created, by AniDB anime ID.</param>
    /// <param name="episodeCreatedAt">When each Shoko episode was created, by AniDB episode ID.</param>
    /// <param name="getXmlWriteTime">Gets when an anime's cached XML was last written, by AniDB anime ID.</param>
    /// <returns>The dates to store.</returns>
    internal static AnidbCreationDatePlan PlanAnidbCreationDates(
        IReadOnlyList<AniDB_Anime> anime,
        IReadOnlyList<AniDB_Episode> episodes,
        IReadOnlyDictionary<int, DateTime> seriesCreatedAt,
        IReadOnlyDictionary<int, DateTime> episodeCreatedAt,
        Func<int, DateTime?> getXmlWriteTime
    )
    {
        var animeDates = anime
            .Where(row => !IsKnownDate(row.CreatedAt))
            .Select(row => (Anime: row, CreatedAt: FirstKnownDate(
                seriesCreatedAt.TryGetValue(row.AnimeID, out var created) ? created : null,
                getXmlWriteTime(row.AnimeID),
                row.DateTimeDescUpdated
            )))
            .Where(row => row.CreatedAt.HasValue)
            .Select(row => (row.Anime, row.CreatedAt!.Value))
            .ToList();

        // An anime's own date, or the one it is given in this run.
        var animeCreatedAt = anime
            .GroupBy(row => row.AnimeID)
            .ToDictionary(group => group.Key, group => FirstKnownDate(group.First().CreatedAt, group.First().DateTimeDescUpdated));
        foreach (var (row, createdAt) in animeDates)
            animeCreatedAt[row.AnimeID] = createdAt;

        var episodeDates = episodes
            .Where(row => !IsKnownDate(row.CreatedAt))
            .Select(row => (Episode: row, CreatedAt: FirstKnownDate(
                episodeCreatedAt.TryGetValue(row.EpisodeID, out var created) ? created : null,
                getXmlWriteTime(row.AnimeID),
                animeCreatedAt.GetValueOrDefault(row.AnimeID)
            )))
            .Where(row => row.CreatedAt.HasValue)
            .Select(row => (row.Episode, row.CreatedAt!.Value))
            .ToList();
        return new(animeDates, episodeDates);
    }

    /// <summary>
    ///   Lists when each anime's cached XML, <c>AnimeDoc_{id}.xml</c>, was
    ///   last written, from one listing of the folder.
    /// </summary>
    /// <param name="directory">The anime XML cache folder.</param>
    /// <returns>The write times in local time, by AniDB anime ID; empty when the folder is missing.</returns>
    internal static IReadOnlyDictionary<int, DateTime> ReadAnimeXmlWriteTimes(string directory)
    {
        var writeTimes = new Dictionary<int, DateTime>();
        if (!Directory.Exists(directory))
            return writeTimes;

        foreach (var file in new DirectoryInfo(directory).EnumerateFiles("AnimeDoc_*.xml"))
        {
            var id = Path.GetFileNameWithoutExtension(file.Name)["AnimeDoc_".Length..];
            if (int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var animeID))
                writeTimes[animeID] = file.LastWriteTime;
        }

        return writeTimes;
    }

    private static bool IsKnownDate(DateTime? date)
        => date > DateTime.UnixEpoch;

    private static DateTime? FirstKnownDate(params DateTime?[] dates)
        => dates.FirstOrDefault(IsKnownDate);

    #endregion
}
