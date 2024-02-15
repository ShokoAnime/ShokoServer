using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.TMDB)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class SearchTMDBSeriesJob : BaseJob
{
    private readonly MovieDBHelper _helper;
    private readonly ISettingsProvider _settingsProvider;
    private string _animeTitle;
    public int AnimeID { get; set; }

    public override void PostInit()
    {
        _animeTitle = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString();
    }

    public override string Name => "Search TMDB Series";
    public override QueueStateStruct Description => new()
    {
        message = "Searching for anime on The MovieDB: {0}",
        queueState = QueueStateEnum.SearchTMDb,
        extraParams = new[] { _animeTitle }
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job} -> Anime: {Anime}", nameof(SearchTMDBSeriesJob), _animeTitle);

        // Use TvDB setting
        var settings = _settingsProvider.GetSettings();
        if (!settings.TvDB.AutoLink)
        {
            return;
        }

        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
        if (anime == null)
        {
            return;
        }

        var searchCriteria = anime.PreferredTitle;

        // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
        var results = await _helper.Search(searchCriteria);
        _logger.LogTrace("Found {Count} moviedb results for {Criteria} on MovieDB", results.Count, searchCriteria);
        if (await ProcessSearchResults(results, searchCriteria))
        {
            return;
        }


        if (results.Count != 0)
        {
            return;
        }

        foreach (var title in anime.GetTitles())
        {
            if (title.TitleType != TitleType.Official)
            {
                continue;
            }

            if (string.Equals(searchCriteria, title.Title, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            results = await _helper.Search(title.Title);
            _logger.LogTrace("Found {Count} moviedb results for search on {Title}", results.Count, title.Title);
            if (await ProcessSearchResults(results, title.Title))
            {
                return;
            }
        }
    }

    private async Task<bool> ProcessSearchResults(List<MovieDB_Movie_Result> results, string searchCriteria)
    {
        if (results.Count == 1)
        {
            // since we are using this result, lets download the info
            _logger.LogTrace("Found 1 moviedb results for search on {SearchCriteria} --- Linked to {Name} ({ID})",
                searchCriteria,
                results[0].MovieName, results[0].MovieID);

            var movieID = results[0].MovieID;
            await _helper.UpdateMovieInfo(movieID, true);
            await _helper.LinkAniDBMovieDB(AnimeID, movieID, false);
            return true;
        }

        return false;
    }

    public SearchTMDBSeriesJob(MovieDBHelper helper, ISettingsProvider settingsProvider)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected SearchTMDBSeriesJob() { }
}
