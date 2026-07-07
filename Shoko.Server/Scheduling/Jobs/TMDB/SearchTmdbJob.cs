using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Scheduling.Acquisition.Attributes;

#pragma warning disable CS8618
using Shoko.Server.Repositories.Cached.AniDB;
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[TmdbApiRateLimited]
[LimitConcurrency(8, 24)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public partial class SearchTmdbJob : BaseJob
{
    private readonly TmdbLinkingService _tmdbLinkingService;

    private readonly TmdbMetadataService _tmdbMetadataService;

    private readonly TmdbSearchService _tmdbSearchService;

    private string _animeTitle;

    public int AnimeID { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public override void PostInit()
    {
        _animeTitle = _anidbAnimes.GetByAnimeID(AnimeID)?.Title ?? AnimeID.ToString();
    }

    public override string TypeName => "Search for TMDB Match";
    public override string Title => "Searching for TMDB Match";
    public override Dictionary<string, object> Details => new() { { "Anime", _animeTitle } };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing SearchTmdbJob for {Anime}: AniDB ID {ID}", _animeTitle ?? AnimeID.ToString(), AnimeID);
        var anime = _anidbAnimes.GetByAnimeID(AnimeID);
        if (anime == null)
        {
            _logger.LogWarning("Anime not found locally: {AnimeID}", AnimeID);
            return;
        }

        if (anime.TmdbShowCrossReferences is { Count: > 0 } || anime.TmdbMovieCrossReferences is { Count: > 0 })
        {
            _logger.LogInformation("Anime already has TMDB links: {AnimeID}", AnimeID);
            return;
        }

        var results = await _tmdbSearchService.SearchForAutoMatch(anime);
        foreach (var result in results)
        {
            if (result.IsMovie)
            {
                _logger.LogInformation("Linking anime {AnimeName} ({AnimeID}), episode {EpisodeName} ({EpisodeID}) to movie {MovieName} ({MovieID})", result.AnidbAnime.PreferredTitle, result.AnidbAnime.AnimeID, result.AnidbEpisode.Title, result.AnidbEpisode.EpisodeID, result.TmdbMovie.OriginalTitle, result.TmdbMovie.ID);
                await _tmdbLinkingService.AddMovieLinkForEpisode(result.AnidbEpisode.EpisodeID, result.TmdbMovie.ID, additiveLink: true, matchRating: result.MatchRating).ConfigureAwait(false);
                await _tmdbMetadataService.ScheduleUpdateOfMovie(new() { MovieId = result.TmdbMovie.ID, ForceRefresh = ForceRefresh, DownloadImages = true }).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation("Linking anime {AnimeName} ({AnimeID}) to show {ShowName} ({ShowID})", result.AnidbAnime.PreferredTitle, result.AnidbAnime.AnimeID, result.TmdbShow.OriginalTitle, result.TmdbShow.ID);
                await _tmdbLinkingService.AddShowLink(result.AnidbAnime.AnimeID, result.TmdbShow.ID, additiveLink: true, matchRating: result.MatchRating).ConfigureAwait(false);
                await _tmdbMetadataService.ScheduleUpdateOfShow(new() { ShowId = result.TmdbShow.ID, ForceRefresh = ForceRefresh, DownloadImages = true }).ConfigureAwait(false);
            }
        }
    }

    private readonly AniDB_AnimeRepository _anidbAnimes;
    public SearchTmdbJob(
        TmdbLinkingService tmdbLinkingService,
        TmdbMetadataService metadataService,
        TmdbSearchService searchService
    ,
        AniDB_AnimeRepository anidbAnimes
    )
    {
        _tmdbLinkingService = tmdbLinkingService;
        _tmdbMetadataService = metadataService;
        _tmdbSearchService = searchService;
        _anidbAnimes = anidbAnimes;

    }

    protected SearchTmdbJob() { }
}
