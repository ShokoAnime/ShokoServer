using System;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.TMDB_Search)]
public class CommandRequest_TMDB_Search : CommandRequestImplementation
{
    private readonly TMDBHelper _helper;
    private readonly ISettingsProvider _settingsProvider;
    public virtual int AnimeID { get; set; }
    public virtual bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Searching for anime on TMDB: {0}",
        queueState = QueueStateEnum.SearchTMDb,
        extraParams = new[] { AnimeID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TMDB_Search: {AnimeID}", AnimeID);
        var settings = _settingsProvider.GetSettings();
        if (!settings.TMDB.AutoLink)
            return;

        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
        if (anime == null)
            return;

        if (anime.AnimeType == (int)AnimeType.Movie)
        {
            SearchForMovies(anime);
            return;
        }

        SearchForShows(anime);
    }

    #region Movie

    private void SearchForMovies(SVR_AniDB_Anime anime)
    {
        // Find the official title in the origin language, to compare it against
        // the original language stored in the offline tmdb search dump.
        var allTitles = anime.GetTitles()
            .Where(title => title.TitleType is TitleType.Main or TitleType.Official);
        var mainTitle = allTitles.FirstOrDefault(x => x.TitleType is TitleType.Main) ?? allTitles.FirstOrDefault();
        var language = mainTitle.Language switch
        {
            TitleLanguage.Romaji => TitleLanguage.Japanese,
            TitleLanguage.Pinyin => TitleLanguage.ChineseSimplified,
            TitleLanguage.KoreanTranscription => TitleLanguage.Korean,
            TitleLanguage.ThaiTranscription => TitleLanguage.Thai,
            _ => mainTitle.Language,
        };
        var officialTitle = language == mainTitle.Language ? mainTitle :
            allTitles.FirstOrDefault(title => title.Language == language) ?? mainTitle;

        // Try to establish a link for every movie (episode) in the movie
        // collection (anime).
        var episodes = anime.GetAniDBEpisodes()
            .Where(episode => episode.EpisodeType == (int)Shoko.Models.Enums.EpisodeType.Episode)
            .ToList();

        // We only have one movie in the movie collection, so don't search for
        // a sub-title.
        if (episodes.Count == 1)
        {
            SearchForMovie(episodes[0], officialTitle.Title);
            return;
        }

        // Find the sub title for each movie in the movie collection, then
        // search for a movie matching the combined title.
        foreach (var episode in episodes)
        {
            var allEpisodeTitles = RepoFactory.AniDB_Episode_Title.GetByEpisodeID(episode.EpisodeID);
            var isCompleteMovie = allEpisodeTitles.Any(title => title.Title.Contains("Complete Movie", StringComparison.InvariantCultureIgnoreCase));
            if (isCompleteMovie)
            {
                SearchForMovie(episode, officialTitle.Title);
                continue;
            }

            var subTitle = allEpisodeTitles.FirstOrDefault(title => title.Language == language) ??
                allEpisodeTitles.FirstOrDefault(title => title.Language == mainTitle.Language);
            var query = $"{officialTitle.Title} {subTitle?.Title ?? ""}".TrimEnd();
            SearchForMovie(episode, query);
        }
    }

    private bool SearchForMovie(AniDB_Episode episode, string query)
    {
        var results = _helper.OfflineSearch.SearchMovies(query).ToList();
        if (results.Count == 0)
            return false;

        Logger.LogTrace("Found {Count} results for search on {Query} --- Linked to {MovieName} ({ID})", results.Count, query, results[0].Title, results[0].ID);

        _helper.AddMovieLink(AnimeID, results[0].ID, episode.EpisodeID, additiveLink: true, isAutomatic: true, forceRefresh: ForceRefresh);

        return true;
    }

    #endregion

    #region Show

    private void SearchForShows(SVR_AniDB_Anime anime)
    {
        // TODO: For later.
    }

    #endregion

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TMDB_Search_{AnimeID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeID = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Search), nameof(AnimeID)));
        ForceRefresh = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Search), nameof(ForceRefresh)));

        return true;
    }

    public CommandRequest_TMDB_Search(ILoggerFactory loggerFactory, TMDBHelper helper, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _helper = helper;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_TMDB_Search()
    {
    }
}
