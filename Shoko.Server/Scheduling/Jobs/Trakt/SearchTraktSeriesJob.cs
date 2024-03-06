using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NHibernate;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Databases;
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
    private readonly TraktTVHelper _helper;
    public virtual int AnimeID { get; set; }

    public override string Name => "Get Trakt Series";

    public override QueueStateStruct Description => new()
    {
        message = "Searching for anime on Trakt.TV: {0}",
        queueState = QueueStateEnum.SearchTrakt,
        extraParams = new[] { AnimeID.ToString() }
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job} -> ID: {ID}", nameof(SearchTraktSeriesJob), AnimeID);
        var settings = _settingsProvider.GetSettings();
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        var sessionWrapper = session.Wrap();
        var doReturn = false;

        // let's try to see locally if we have a tvDB link for this anime
        // Trakt allows the use of TvDB ID's or their own Trakt ID's
        var xrefTvDBs = RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(AnimeID);
        if (xrefTvDBs is { Count: > 0 })
        {
            foreach (var tvXRef in xrefTvDBs)
            {
                // first search for this show by the TvDB ID
                var searchResults =
                    _helper.SearchShowByIDV2(TraktSearchIDType.tvdb,
                        tvXRef.TvDBID.ToString());
                if (searchResults == null || searchResults.Count <= 0) continue;

                // since we are searching by ID, there will only be one 'show' result
                TraktV2Show resShow = null;
                foreach (var res in searchResults)
                {
                    if (res.ResultType != SearchIDType.Show) continue;

                    resShow = res.show;
                    break;
                }

                if (resShow == null) continue;

                var showInfo = _helper.GetShowInfoV2(resShow.ids.slug);
                if (showInfo?.ids == null) continue;

                // make sure the season specified by TvDB also exists on Trakt
                var traktShow =
                    RepoFactory.Trakt_Show.GetByTraktSlug(session, showInfo.ids.slug);
                if (traktShow == null) continue;

                var traktSeason = RepoFactory.Trakt_Season.GetByShowIDAndSeason(
                    session,
                    traktShow.Trakt_ShowID,
                    tvXRef.TvDBSeasonNumber);
                if (traktSeason == null) continue;

                _logger.LogTrace("Found trakt match using TvDBID locally {AnimeID} - id = {Title}",
                    AnimeID, showInfo.title);
                _helper.LinkAniDBTrakt(AnimeID,
                    (EpisodeType)tvXRef.AniDBStartEpisodeType,
                    tvXRef.AniDBStartEpisodeNumber, showInfo.ids.slug,
                    tvXRef.TvDBSeasonNumber, tvXRef.TvDBStartEpisodeNumber,
                    true);
                doReturn = true;
            }

            if (doReturn) return Task.CompletedTask;
        }

        // Use TvDB setting due to similarity
        if (!settings.TvDB.AutoLink) return Task.CompletedTask;

        // finally lets try searching Trakt directly
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, AnimeID);
        if (anime == null) return Task.CompletedTask;

        var searchCriteria = anime.MainTitle;

        // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
        var results = _helper.SearchShowV2(searchCriteria);
        _logger.LogTrace("Found {Count} trakt results for {Criteria} ", results.Count, searchCriteria);
        if (ProcessSearchResults(session, results, searchCriteria)) return Task.CompletedTask;

        if (results.Count != 0) return Task.CompletedTask;

        foreach (var title in anime.GetTitles())
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
            if (results[0].show != null)
            {
                // since we are using this result, lets download the info
                _logger.LogTrace("Found 1 trakt results for search on {Query} --- Linked to {Title} ({ID})", searchCriteria,
                    results[0].show.Title, results[0].show.ids.slug);
                var showInfo = _helper.GetShowInfoV2(results[0].show.ids.slug);
                if (showInfo != null)
                {
                    _helper.LinkAniDBTrakt(session, AnimeID, EpisodeType.Episode, 1,
                        results[0].show.ids.slug, 1, 1,
                        true);
                    return true;
                }
            }
        }

        return false;
    }

    public SearchTraktSeriesJob(TraktTVHelper helper, ISettingsProvider settingsProvider)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected SearchTraktSeriesJob() { }
}
