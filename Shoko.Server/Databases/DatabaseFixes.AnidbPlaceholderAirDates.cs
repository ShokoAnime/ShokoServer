using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using Shoko.Abstractions.Core.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Repositories;
using Shoko.Server.Services;

#pragma warning disable CS0618
namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region AniDB Placeholder Air Dates | Steps

    /// <summary>
    ///   Puts AniDB's 1970-01-01 placeholder back on the episodes whose cached
    ///   anime XML gives it, after the schema steps made every stored <c>0</c>
    ///   an episode without an air date. Only anime with an undated episode
    ///   are read.
    /// </summary>
    public static void RestoreAnidbPlaceholderAirDates()
    {
        var services = ISystemService.StaticServices;
        var systemService = services.GetRequiredService<SystemService>();
        var xmlUtils = services.GetRequiredService<HttpXmlUtils>();
        var parser = services.GetRequiredService<HttpAnimeParser>();
        // Only anime starting by 1970-01-01, or at an unknown date, can hold the placeholder.
        var epoch = new DateOnly(1970, 1, 1);
        var undatedByAnime = RepoFactory.AniDB_Episode.GetAll()
            .Where(episode => episode.AirDate is null)
            .GroupBy(episode => episode.AnimeID)
            .Where(group => RepoFactory.AniDB_Anime.GetByAnimeID(group.Key)?.AirDate?.ToDateOnly() is not { } start || start <= epoch)
            .ToDictionary(group => group.Key, group => group.ToList());
        if (undatedByAnime.Count is 0)
        {
            _logger.Info("Restored AniDB placeholder air dates: no episode is undated.");
            return;
        }

        var str = systemService.StartupMessage ?? string.Empty;
        var progress = new StartupProgress(ReportTo(systemService, str), "Restoring AniDB placeholder air dates", undatedByAnime.Count);
        var placeholders = new List<AniDB_Episode>();
        var unread = 0;
        foreach (var (animeID, undated) in undatedByAnime)
        {
            var found = FindPlaceholderAirDates(
                animeID,
                undated,
                id => xmlUtils.LoadAnimeHTTPFromFile(id).GetAwaiter().GetResult(),
                parser
            );
            if (found is null)
                unread++;
            else
                placeholders.AddRange(found);

            progress.Advance();
        }

        if (unread > 0)
            _logger.Warn($"Unable to restore the AniDB placeholder air dates of {unread} anime without a usable cached Anime_HTTP xml dump; they are restored when the anime is next updated.");

        if (placeholders.Count > 0)
        {
            var databaseFactory = services.GetRequiredService<DatabaseFactory>();
            using var session = databaseFactory.SessionFactory.OpenStatelessSession();
            WriteInTransaction(
                session,
                () => UpdateRowsByKey(
                    session,
                    "AniDB_Episode",
                    "AniDB_EpisodeID",
                    NHibernateUtil.Int32,
                    placeholders,
                    episode => episode.AniDB_EpisodeID,
                    [("AirDate", NHibernateUtil.Int32, _ => 0)]
                )
            );
            foreach (var episode in placeholders)
                episode.AirDate = 0;
        }

        _logger.Info($"Restored the AniDB 1970-01-01 placeholder air date of {placeholders.Count} episodes from {undatedByAnime.Count} anime.");
        systemService.StartupMessage = $"{str} - Restored AniDB placeholder air dates for {placeholders.Count} episodes.";
    }

    /// <summary>
    ///   Finds the undated episodes of one anime that its cached XML gives
    ///   AniDB's 1970-01-01 placeholder. A missing or unreadable XML is
    ///   logged at the debug level.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <param name="undated">The anime's episodes stored without an air date.</param>
    /// <param name="loadXml">Reads an anime's cached XML, or gives <c>null</c> when there is none.</param>
    /// <param name="parser">The anime XML parser.</param>
    /// <returns>The episodes to give the placeholder, or <c>null</c> when the XML is missing or unreadable.</returns>
    internal static IReadOnlyList<AniDB_Episode>? FindPlaceholderAirDates(
        int animeID,
        IReadOnlyList<AniDB_Episode> undated,
        Func<int, string?> loadXml,
        HttpAnimeParser parser
    )
    {
        try
        {
            var xml = loadXml(animeID);
            if (string.IsNullOrEmpty(xml))
            {
                _logger.Debug($"Unable to restore placeholder air dates for anime {animeID}: no cached Anime_HTTP xml dump.");
                return null;
            }

            if (parser.Parse(animeID, xml) is not { } response)
            {
                _logger.Debug($"Unable to restore placeholder air dates for anime {animeID}: the cached Anime_HTTP xml dump is unusable.");
                return null;
            }

            var placeholderIDs = response.Episodes
                .Where(episode => AniDBExtensions.GetAniDBAirDateAsSeconds(episode.AirDate) is 0)
                .Select(episode => episode.EpisodeID)
                .ToHashSet();
            return undated
                .Where(episode => episode.AirDate is null && placeholderIDs.Contains(episode.EpisodeID))
                .ToList();
        }
        catch (Exception e)
        {
            _logger.Debug(e, $"Unable to restore placeholder air dates for anime {animeID}: the cached Anime_HTTP xml dump is broken.");
            return null;
        }
    }

    #endregion
}
