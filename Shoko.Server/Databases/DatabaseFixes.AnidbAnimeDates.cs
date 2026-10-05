using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Repositories;
using Shoko.Server.Services;

#pragma warning disable CS0618
namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region AniDB Anime Dates | Types

    /// <summary>
    ///   What restoring the AniDB anime dates from the cached anime XML did.
    /// </summary>
    /// <param name="Fixed">The anime whose dates were corrected and saved.</param>
    /// <param name="Unchanged">The anime whose stored dates already matched their cached XML.</param>
    /// <param name="Queued">The anime stored with the placeholder, queued for a refresh for want of a readable cached XML.</param>
    /// <param name="Skipped">The anime without a date and without a readable cached XML, left alone.</param>
    internal sealed record AnidbAnimeDatesResult(int Fixed, int Unchanged, int Queued, int Skipped);

    #endregion

    #region AniDB Anime Dates | Steps

    /// <summary>
    ///   AniDB's placeholder for an unknown anime start or end date, which
    ///   older versions stored as a real date.
    /// </summary>
    private static readonly PartialDateOnly _anidbPlaceholderDate = new(1970, 1, 1);

    /// <summary>
    ///   Reads the start and end dates of the AniDB anime stored without a
    ///   start date or with the 1970-01-01 placeholder back from their cached
    ///   XML, queues a refresh of those holding the placeholder without one,
    ///   and updates the series and group stats the dates feed.
    /// </summary>
    public static void RestoreAnidbAnimeDates()
    {
        var services = ISystemService.StaticServices;
        var systemService = services.GetRequiredService<SystemService>();
        var xmlUtils = services.GetRequiredService<HttpXmlUtils>();
        var parser = services.GetRequiredService<HttpAnimeParser>();
        var scheduler = services.GetRequiredService<IQueueScheduler>();
        var animeList = RepoFactory.AniDB_Anime.GetAll()
            .Where(anime => anime.AirDate is null || HasAnidbPlaceholderDate(anime))
            .ToList();
        if (animeList.Count is 0)
        {
            _logger.Info("Restored AniDB anime dates: no anime is undated or holds the placeholder.");
            return;
        }

        var str = systemService.StartupMessage ?? string.Empty;
        var progress = new StartupProgress(ReportTo(systemService, str), "Restoring AniDB anime dates from the cached anime XML", animeList.Count);
        var changed = new List<AniDB_Anime>();
        var result = FixAnidbAnimeDates(
            animeList,
            animeID => xmlUtils.LoadAnimeHTTPFromFile(animeID).GetAwaiter().GetResult(),
            parser,
            anime =>
            {
                RepoFactory.AniDB_Anime.Save(anime);
                changed.Add(anime);
            },
            animeID => QueueAnidbAnimeRefresh(scheduler, animeID),
            () => progress.Advance()
        );

        UpdateStatsForAnidbAnime(changed);

        _logger.Info(
            $"Restored the start and end dates of {result.Fixed} AniDB anime from the cached anime XML; {result.Unchanged} were unchanged, " +
            $"{result.Queued} holding the placeholder without a readable cached xml dump were queued for a refresh, " +
            $"and {result.Skipped} undated were left alone."
        );
        systemService.StartupMessage = $"{str} - Restored AniDB anime dates for {result.Fixed} anime.";
    }

    /// <summary>
    ///   Corrects the stored dates of each anime from its cached XML, saving
    ///   only the anime whose dates differ, and hands the anime holding the
    ///   placeholder without a readable XML to <paramref name="queueRefresh"/>.
    /// </summary>
    /// <param name="animeList">The AniDB anime to correct.</param>
    /// <param name="loadXml">Reads an anime's cached XML, or gives <c>null</c> when there is none.</param>
    /// <param name="parser">The anime XML parser.</param>
    /// <param name="save">Saves an anime whose dates were corrected.</param>
    /// <param name="queueRefresh">Queues a refresh of an anime.</param>
    /// <param name="advance">Called once per anime done, or <c>null</c>.</param>
    /// <returns>What was fixed, left unchanged, queued and skipped.</returns>
    internal static AnidbAnimeDatesResult FixAnidbAnimeDates(
        IReadOnlyList<AniDB_Anime> animeList,
        Func<int, string?> loadXml,
        HttpAnimeParser parser,
        Action<AniDB_Anime> save,
        Action<int> queueRefresh,
        Action? advance = null
    )
    {
        var fixedCount = 0;
        var unchanged = 0;
        var queued = 0;
        var skipped = 0;
        foreach (var anime in animeList)
        {
            try
            {
                if (ReadCachedAnidbAnime(anime.AnimeID, loadXml, parser) is not { } response)
                {
                    if (HasAnidbPlaceholderDate(anime))
                    {
                        _logger.Debug($"Queued a refresh of anime {anime.AnimeID} to restore its dates: it holds the placeholder.");
                        queueRefresh(anime.AnimeID);
                        queued++;
                    }
                    else
                    {
                        _logger.Debug($"Left the dates of anime {anime.AnimeID} alone: it is undated.");
                        skipped++;
                    }

                    continue;
                }

                var dates = response.Anime;
                if (anime.AirDate == dates.AirDate && anime.EndDate == dates.EndDate && anime.BeginYear == dates.BeginYear && anime.EndYear == dates.EndYear)
                {
                    unchanged++;
                    continue;
                }

                anime.AirDate = dates.AirDate;
                anime.EndDate = dates.EndDate;
                anime.BeginYear = dates.BeginYear;
                anime.EndYear = dates.EndYear;
                anime.ResetReleaseStatus();
                anime.ResetRegularAirDates();
                anime.ResetYearlySeasons();
                save(anime);
                fixedCount++;
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Unable to restore the dates of anime: {anime.AnimeID}");
            }
            finally
            {
                advance?.Invoke();
            }
        }

        return new(fixedCount, unchanged, queued, skipped);
    }

    /// <summary>
    ///   Whether an anime's stored start or end date is AniDB's 1970-01-01
    ///   placeholder for an unknown date.
    /// </summary>
    /// <param name="anime">The AniDB anime.</param>
    /// <returns><c>true</c> when either date is the placeholder.</returns>
    private static bool HasAnidbPlaceholderDate(AniDB_Anime anime)
        => anime.AirDate == _anidbPlaceholderDate || anime.EndDate == _anidbPlaceholderDate;

    /// <summary>
    ///   Parses an anime's cached XML. A missing, unusable or broken XML is
    ///   logged at the debug level.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <param name="loadXml">Reads an anime's cached XML, or gives <c>null</c> when there is none.</param>
    /// <param name="parser">The anime XML parser.</param>
    /// <returns>The parsed anime, or <c>null</c> when the XML is missing or unreadable.</returns>
    private static ResponseGetAnime? ReadCachedAnidbAnime(int animeID, Func<int, string?> loadXml, HttpAnimeParser parser)
    {
        try
        {
            var xml = loadXml(animeID);
            if (string.IsNullOrEmpty(xml))
            {
                _logger.Debug($"Unable to restore the dates of anime {animeID} from its cache: no cached Anime_HTTP xml dump.");
                return null;
            }

            var response = parser.Parse(animeID, xml);
            if (response is null)
                _logger.Debug($"Unable to restore the dates of anime {animeID} from its cache: the cached Anime_HTTP xml dump is unusable.");

            return response;
        }
        catch (Exception e)
        {
            _logger.Debug(e, $"Unable to restore the dates of anime {animeID} from its cache: the cached Anime_HTTP xml dump is broken.");
            return null;
        }
    }

    /// <summary>
    ///   Updates the missing episode stats of the series of each anime, and
    ///   of their top level groups, which fall back on the anime's start date
    ///   for undated episodes.
    /// </summary>
    /// <param name="animeList">The AniDB anime whose dates changed.</param>
    private static void UpdateStatsForAnidbAnime(IReadOnlyList<AniDB_Anime> animeList)
    {
        if (animeList.Count is 0)
            return;

        var services = ISystemService.StaticServices;
        var seriesService = services.GetRequiredService<AnimeSeriesService>();
        var groupService = services.GetRequiredService<AnimeGroupService>();
        var groups = new Dictionary<int, AnimeGroup>();
        foreach (var anime in animeList)
        {
            if (RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID) is not { } series)
                continue;

            try
            {
                seriesService.UpdateStats(series, false, true);
                var group = series.TopLevelAnimeGroup;
                groups.TryAdd(group.AnimeGroupID, group);
            }
            catch (Exception e)
            {
                _logger.Warn(e, $"Unable to update the stats of series {series.AnimeSeriesID} after restoring the dates of anime {anime.AnimeID}.");
            }
        }

        foreach (var group in groups.Values)
        {
            try
            {
                groupService.UpdateStatsFromTopLevel(group, false, true);
            }
            catch (Exception e)
            {
                _logger.Warn(e, $"Unable to update the stats of group {group.AnimeGroupID} after restoring AniDB anime dates.");
            }
        }
    }

    #endregion
}
