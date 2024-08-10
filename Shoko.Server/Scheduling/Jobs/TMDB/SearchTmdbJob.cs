using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

#nullable enable
#pragma warning disable CS8618
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(8, 24)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public partial class SearchTmdbJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbMetadataService;

    private readonly TmdbSearchService _tmdbSearchService;

    private string _animeTitle;

    public int AnimeID { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public override void PostInit()
    {
        _animeTitle = RepoFactory.AniDB_Anime?.GetByAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString();
    }

    public override string TypeName => "Search for TMDB Match";
    public override string Title => "Searching for TMDB Match";
    public override Dictionary<string, object> Details => new() { { "Anime", _animeTitle } };

    public override async Task Process()
    {
        _logger.LogInformation("Processing SearchTmdbJob for {Anime}: AniDB ID {ID}", _animeTitle ?? AnimeID.ToString(), AnimeID);
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
        if (anime == null)
            return;

        var results = await _tmdbSearchService.SearchForAutoMatch(anime);
        foreach (var result in results)
        {
            if (result.IsMovie)
            {
                _logger.LogInformation("Linking anime {AnimeName} ({AnimeID}), episode {EpisodeName} ({EpisodeID}) to movie {MovieName} ({MovieID})", result.AnidbAnime.PreferredTitle, result.AnidbAnime.AnimeID, result.AnidbEpisode.PreferredTitle, result.AnidbEpisode.EpisodeID, result.TmdbMovie.OriginalTitle, result.TmdbMovie.Id);
                await _tmdbMetadataService.AddMovieLink(result.AnidbAnime.AnimeID, result.TmdbMovie.Id, result.AnidbEpisode.EpisodeID, additiveLink: true, isAutomatic: true).ConfigureAwait(false);
                await _tmdbMetadataService.ScheduleUpdateOfMovie(result.TmdbMovie.Id, forceRefresh: ForceRefresh, downloadImages: true).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation("Linking anime {AnimeName} ({AnimeID}) to show {ShowName} ({ShowID})", result.AnidbAnime.PreferredTitle, result.AnidbAnime.AnimeID, result.TmdbShow.OriginalName, result.TmdbShow.Id);
                await _tmdbMetadataService.AddShowLink(result.AnidbAnime.AnimeID, result.TmdbShow.Id, additiveLink: true, isAutomatic: true).ConfigureAwait(false);
                await _tmdbMetadataService.ScheduleUpdateOfShow(result.TmdbShow.Id, forceRefresh: ForceRefresh, downloadImages: true).ConfigureAwait(false);
            }
        }
    }

    public SearchTmdbJob(TmdbMetadataService metadataService, TmdbSearchService searchService)
    {
        _tmdbMetadataService = metadataService;
        _tmdbSearchService = searchService;
    }

    protected SearchTmdbJob() { }
}
