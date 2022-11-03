using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.TvDB_SearchAnime)]
public class CommandRequest_TvDBSearchAnime : CommandRequestImplementation
{
    private readonly TvDBApiHelper _helper;
    public int AnimeID { get; set; }
    public bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Searching for anime on The TvDB: {0}",
        queueState = QueueStateEnum.SearchTvDB,
        extraParams = new[] { AnimeID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TvDBSearchAnime: {0}", AnimeID);

        try
        {
            if (!ServerSettings.Instance.TvDB.AutoLink)
            {
                return;
            }

            // try to pull a link from a prequel/sequel
            var relations = RepoFactory.AniDB_Anime_Relation.GetFullLinearRelationTree(AnimeID);
            var tvDBID = relations.SelectMany(a => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(a))
                .FirstOrDefault(a => a != null)?.TvDBID;

            if (tvDBID != null)
            {
                _helper.LinkAniDBTvDB(AnimeID, tvDBID.Value, true);
                return;
            }

            // search TvDB
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
            if (anime == null)
            {
                return;
            }

            var searchCriteria = CleanTitle(anime.MainTitle);

            // if not wanting to use web cache, or no match found on the web cache go to TvDB directly
            var results = _helper.SearchSeries(searchCriteria);
            Logger.LogTrace("Found {0} tvdb results for {1} on TheTvDB", results.Count, searchCriteria);
            if (ProcessSearchResults(results, searchCriteria))
            {
                return;
            }


            if (results.Count != 0)
            {
                return;
            }

            var foundResult = false;
            foreach (var title in anime.GetTitles())
            {
                if (title.TitleType != TitleType.Official)
                {
                    continue;
                }

                if (title.Language != TitleLanguage.English && title.Language != TitleLanguage.Romaji)
                {
                    continue;
                }

                var cleanTitle = CleanTitle(title.Title);

                if (searchCriteria.Equals(cleanTitle, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                searchCriteria = cleanTitle;
                results = _helper.SearchSeries(searchCriteria);
                if (results.Count > 0)
                {
                    foundResult = true;
                }

                Logger.LogTrace("Found {0} tvdb results for search on {1}", results.Count, searchCriteria);
                if (ProcessSearchResults(results, searchCriteria))
                {
                    return;
                }
            }

            if (!foundResult)
            {
                Logger.LogWarning("Unable to find a matching TvDB series for {0}", anime.MainTitle);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error processing CommandRequest_TvDBSearchAnime: {0} - {1}", AnimeID, ex);
        }
    }

    private bool ProcessSearchResults(List<TVDB_Series_Search_Response> results, string searchCriteria)
    {
        TvDB_Series tvser;
        switch (results.Count)
        {
            case 1:
                // since we are using this result, lets download the info
                Logger.LogTrace("Found 1 tvdb results for search on {0} --- Linked to {1} ({2})", searchCriteria,
                    results[0].SeriesName,
                    results[0].SeriesID);
                tvser = _helper.GetSeriesInfoOnline(results[0].SeriesID, false);
                _helper.LinkAniDBTvDB(AnimeID, results[0].SeriesID, true);

                // add links for multiple seasons (for long shows)
                AddCrossRef_AniDB_TvDBV2(AnimeID, results[0].SeriesID, CrossRefSource.Automatic);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
                return true;
            case 0:
                return false;
            default:
                Logger.LogTrace("Found multiple ({0}) tvdb results for search on so checking for english results {1}",
                    results.Count,
                    searchCriteria);
                foreach (var sres in results)
                {
                    // since we are using this result, lets download the info
                    Logger.LogTrace("Found english result for search on {0} --- Linked to {1} ({2})", searchCriteria,
                        sres.SeriesName,
                        sres.SeriesID);
                    tvser = _helper.GetSeriesInfoOnline(results[0].SeriesID, false);
                    _helper.LinkAniDBTvDB(AnimeID, sres.SeriesID, true);

                    // add links for multiple seasons (for long shows)
                    AddCrossRef_AniDB_TvDBV2(AnimeID, results[0].SeriesID, CrossRefSource.Automatic);
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
                    return true;
                }

                Logger.LogTrace("No english results found, so SKIPPING: {0}", searchCriteria);

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

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TvDBSearchAnime{AnimeID}";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;

        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBSearchAnime", "AnimeID"));
        ForceRefresh =
            bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBSearchAnime", "ForceRefresh"));

        return true;
    }

    public override CommandRequest ToDatabaseObject()
    {
        GenerateCommandID();

        var cq = new CommandRequest
        {
            CommandID = CommandID,
            CommandType = CommandType,
            Priority = Priority,
            CommandDetails = ToXML(),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_TvDBSearchAnime(ILoggerFactory loggerFactory, TvDBApiHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TvDBSearchAnime()
    {
    }
}
