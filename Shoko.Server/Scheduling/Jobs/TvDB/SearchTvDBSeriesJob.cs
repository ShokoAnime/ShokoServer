using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.TvDB;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.TvDB)]
[JobKeyGroup(JobKeyGroup.TvDB)]
public class SearchTvDBSeriesJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly JobFactory _jobFactory;
    private readonly TvDBApiHelper _helper;
    private string _title;

    public int AnimeID { get; set; }
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Search for TvDB Series";
    public override string Title => "Searching for TvDB Series";

    public override void PostInit()
    {
        _title = RepoFactory.AniDB_Anime?.GetByAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString();
    }

    public override Dictionary<string, object> Details => new() { { "Anime", _title } };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {ID}", nameof(SearchTvDBSeriesJob), _title);

        var settings = _settingsProvider.GetSettings();
        if (!settings.TvDB.AutoLink) return;

        // try to pull a link from a prequel/sequel
        var relations = RepoFactory.AniDB_Anime_Relation.GetFullLinearRelationTree(AnimeID);
        var tvDBID = relations.SelectMany(a => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(a))
            .FirstOrDefault(a => a != null)?.TvDBID;

        if (tvDBID != null)
        {
            await _helper.LinkAniDBTvDB(AnimeID, tvDBID.Value, true, true);
            return;
        }

        // search TvDB
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
        if (anime == null) return;

        var searchCriteria = CleanTitle(anime.MainTitle);

        // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
        var results = await _helper.SearchSeriesAsync(searchCriteria);
        _logger.LogTrace("Found {Count} tvdb results for {Query} on TheTvDB", results.Count, searchCriteria);
        if (await ProcessSearchResults(results, searchCriteria)) return;
        if (results.Count != 0) return;

        var foundResult = false;
        foreach (var title in anime.Titles)
        {
            if (title.TitleType != TitleType.Official) continue;
            if (title.Language != TitleLanguage.English && title.Language != TitleLanguage.Romaji) continue;

            var cleanTitle = CleanTitle(title.Title);
            if (string.Equals(searchCriteria, cleanTitle, StringComparison.InvariantCultureIgnoreCase)) continue;

            searchCriteria = cleanTitle;
            results = await _helper.SearchSeriesAsync(searchCriteria);
            if (results.Count > 0) foundResult = true;

            _logger.LogTrace("Found {Count} tvdb results for search on {Query}", results.Count, searchCriteria);
            if (await ProcessSearchResults(results, searchCriteria)) return;
        }

        if (!foundResult)
        {
            _logger.LogWarning("Unable to find a matching TvDB series for {Query}", _title);
        }
    }

    private async Task<bool> ProcessSearchResults(List<TVDB_Series_Search_Response> results, string searchCriteria)
    {
        switch (results.Count)
        {
            case 1:
                // since we are using this result, lets download the info
                _logger.LogTrace("Found 1 tvdb results for {Query} --- Linked to {Name} ({ID})", searchCriteria, results[0].SeriesName, results[0].SeriesID);
                await _helper.GetSeriesInfoOnlineAsync(results[0].SeriesID, false);
                await _helper.LinkAniDBTvDB(AnimeID, results[0].SeriesID, true, true);

                // add links for multiple seasons (for long shows)
                AddCrossRef_AniDB_TvDBV2(AnimeID, results[0].SeriesID, CrossRefSource.Automatic);
                await _jobFactory.CreateJob<RefreshAnimeStatsJob>(a => a.AnimeID = AnimeID).Process();
                return true;
            case 0:
                return false;
            default:
                _logger.LogTrace("Found multiple ({Count}) tvdb results for {Query}, so checking for english results", results.Count, searchCriteria);
                foreach (var sres in results)
                {
                    // since we are using this result, lets download the info
                    _logger.LogTrace("Found english result for {Query} --- Linked to {Name} ({ID})", searchCriteria, sres.SeriesName, sres.SeriesID);
                    await _helper.GetSeriesInfoOnlineAsync(results[0].SeriesID, false);
                    await _helper.LinkAniDBTvDB(AnimeID, sres.SeriesID, true, true);

                    // add links for multiple seasons (for long shows)
                    AddCrossRef_AniDB_TvDBV2(AnimeID, results[0].SeriesID, CrossRefSource.Automatic);
                    await _jobFactory.CreateJob<RefreshAnimeStatsJob>(a => a.AnimeID = AnimeID).Process();
                    return true;
                }

                _logger.LogTrace("No english results found, so SKIPPING: {Query}", searchCriteria);

                return false;
        }
    }

    private static void AddCrossRef_AniDB_TvDBV2(int animeID, int tvdbID, CrossRefSource source)
    {
        var xref =
            RepoFactory.CrossRef_AniDB_TvDB.GetByAniDBAndTvDBID(animeID, tvdbID);
        if (xref != null)
        {
            return;
        }

        xref = new CrossRef_AniDB_TvDB { AniDBID = animeID, TvDBID = tvdbID, CrossRefSource = source };
        RepoFactory.CrossRef_AniDB_TvDB.Save(xref);
    }

    private static readonly Regex RemoveYear = new(@"(^.*)( \([0-9]+\)$)", RegexOptions.Compiled);
    private static readonly Regex RemoveAfterColon = new(@"(^.*)(\:.*$)", RegexOptions.Compiled);

    private static string CleanTitle(string title)
    {
        var result = RemoveYear.Replace(title, "$1");
        result = RemoveAfterColon.Replace(result, "$1");
        return result;
    }

    public SearchTvDBSeriesJob(TvDBApiHelper helper, ISettingsProvider settingsProvider, JobFactory jobFactory)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
        _jobFactory = jobFactory;
    }

    protected SearchTvDBSeriesJob() { }
}
