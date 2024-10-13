using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NHibernate;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Databases;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Settings;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.Scheduling.Jobs.Trakt;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.Trakt)]
[JobKeyGroup(JobKeyGroup.Trakt)]
public class SearchTraktSeriesJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly AniDBTitleHelper _titleHelper;
    private readonly TraktTVHelper _helper;
    private readonly DatabaseFactory _databaseFactory;
    private string _anime;

    public int AnimeID { get; set; }

    public override string TypeName => "Search for Trakt Series";
    public override string Title => "Searching for Trakt Series";
    public override void PostInit()
    {
        _anime = RepoFactory.AniDB_Anime?.GetByAnimeID(AnimeID)?.PreferredTitle ?? _titleHelper.SearchAnimeID(AnimeID)?.PreferredTitle;
    }
    public override Dictionary<string, object> Details => new() { { "Anime", _anime ?? AnimeID.ToString() } };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} -> ID: {ID}", nameof(SearchTraktSeriesJob), AnimeID);
        var settings = _settingsProvider.GetSettings();
        using var session = _databaseFactory.SessionFactory.OpenSession();
        var sessionWrapper = session.Wrap();
        var doReturn = false;

        // let's try to see locally if we have a tmdb link for this anime
        // Trakt allows the use of tmdb ID's or their own Trakt ID's
        if (RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(AnimeID) is { Count: > 0 } tmdbXrefs)
        {
            foreach (var tmdbXref in tmdbXrefs)
            {
                // first search for this show by the tmdb ID
                var searchResults = _helper.SearchShowByTmdbId(tmdbXref.TmdbShowID);
                var resShow = searchResults.FirstOrDefault()?.Show;
                if (resShow == null) continue;

                var showInfo = _helper.GetShowInfoV2(resShow.IDs.TraktSlug);
                if (showInfo?.IDs == null) continue;

                // make sure the season specified by tmdb also exists on Trakt
                var traktShow = RepoFactory.Trakt_Show.GetByTraktSlug(session, showInfo.IDs.TraktSlug);
                if (traktShow == null) continue;

                var episodeXrefs = RepoFactory.CrossRef_AniDB_TMDB_Episode.GetAllByAnidbAnimeAndTmdbShowIDs(AnimeID, tmdbXref.TmdbShowID)
                    .Select(xref => new { xref, tmdb = xref.TmdbEpisode, anidb = xref.AnidbEpisode })
                    .Where(xref => xref.tmdb != null && xref.anidb != null)
                    .ToList();
                var seasonNumber = episodeXrefs.GroupBy(x => x.tmdb.SeasonNumber).MaxBy(x => x.Count())?.Key ?? -1;
                if (seasonNumber == -1) continue;
                var traktSeason = RepoFactory.Trakt_Season.GetByShowIDAndSeason(
                    session,
                    traktShow.Trakt_ShowID,
                    seasonNumber);
                if (traktSeason == null) continue;

                var firstEpisode = episodeXrefs.Where(x => x.tmdb.SeasonNumber == seasonNumber).MinBy(x => x.tmdb.EpisodeNumber);
                if (firstEpisode == null) continue;

                _logger.LogTrace("Found trakt match using local TMDB Show: {Trakt Title} (AnidbAnime={AnidbAnimeID},TmdbShow={TmdbShowID})", showInfo.Title, AnimeID, tmdbXref.TmdbShowID);
                _helper.LinkAniDBTrakt(AnimeID,
                    firstEpisode.anidb.EpisodeTypeEnum,
                    firstEpisode.anidb.EpisodeNumber,
                    showInfo.IDs.TraktSlug,
                    firstEpisode.tmdb.SeasonNumber,
                    firstEpisode.tmdb.EpisodeNumber,
                    true);
                doReturn = true;
            }

            if (doReturn) return Task.CompletedTask;
        }

        // finally lets try searching Trakt directly
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, AnimeID);
        if (anime == null) return Task.CompletedTask;

        var searchCriteria = anime.MainTitle;

        // if not wanting to use web cache, or no match found on the web cache go to tmdb directly
        var results = _helper.SearchShowV2(searchCriteria);
        _logger.LogTrace("Found {Count} trakt results for {Criteria} ", results.Count, searchCriteria);
        if (ProcessSearchResults(session, results, searchCriteria)) return Task.CompletedTask;

        if (results.Count != 0) return Task.CompletedTask;

        foreach (var title in anime.Titles)
        {
            if (title.TitleType != TitleType.Official) continue;

            if (string.Equals(searchCriteria, title.Title, StringComparison.InvariantCultureIgnoreCase)) continue;

            results = _helper.SearchShowV2(searchCriteria);
            _logger.LogTrace("Found {Count} trakt results for search on {Title}", results.Count, title.Title);
            if (ProcessSearchResults(session, results, title.Title)) return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private bool ProcessSearchResults(ISession session, List<TraktV2SearchShowResult> results,
        string searchCriteria)
    {
        if (results.Count == 1)
        {
            if (results[0].Show != null)
            {
                // since we are using this result, lets download the info
                _logger.LogTrace("Found 1 trakt results for search on {Query} --- Linked to {Title} ({ID})", searchCriteria,
                    results[0].Show.Title, results[0].Show.IDs.TraktSlug);
                var showInfo = _helper.GetShowInfoV2(results[0].Show.IDs.TraktSlug);
                if (showInfo != null)
                {
                    _helper.LinkAniDBTrakt(session, AnimeID, EpisodeType.Episode, 1,
                        results[0].Show.IDs.TraktSlug, 1, 1,
                        true);
                    return true;
                }
            }
        }

        return false;
    }

    public SearchTraktSeriesJob(TraktTVHelper helper, ISettingsProvider settingsProvider, AniDBTitleHelper titleHelper, DatabaseFactory databaseFactory)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
        _titleHelper = titleHelper;
        _databaseFactory = databaseFactory;
    }

    protected SearchTraktSeriesJob() { }
}
