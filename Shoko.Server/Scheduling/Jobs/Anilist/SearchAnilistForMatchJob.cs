using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.Anilist;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Anilist;
using Shoko.Server.Scheduling.Acquisition.Attributes;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Anilist;

[DatabaseRequired]
[AnilistApiRateLimited]
[LimitConcurrency(8, 24)]
[JobKeyGroup(JobKeyGroup.Anilist)]
public class SearchAnilistForMatchJob : BaseJob
{
    private readonly AnilistLinkingService _anilistLinkingService;

    private readonly AnilistMetadataService _anilistMetadataService;

    private readonly AnilistSearchService _anilistSearchService;

    private readonly AniDB_AnimeRepository _anidbAnime;

    private readonly CrossRef_AniDB_Anilist_AnimeRepository _xrefAnidbAnilistAnime;

    private string? _animeTitle;

    public int AnimeID { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public override void PostInit()
    {
        _animeTitle = _anidbAnime.GetByAnimeID(AnimeID)?.MainTitle ?? AnimeID.ToString();
    }

    public override string TypeName => "Search for AniList Match";

    public override string Title => "Searching for AniList Match";

    public override Dictionary<string, object> Details => new() { { "Anime", _animeTitle ?? AnimeID.ToString() } };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing SearchAnilistForMatchJob for {Anime}: AniDB ID {ID}", _animeTitle ?? AnimeID.ToString(), AnimeID);

        var anime = _anidbAnime.GetByAnimeID(AnimeID);
        if (anime is null)
        {
            _logger.LogWarning("Anime not found locally: {AnimeID}", AnimeID);
            return;
        }

        // Check if already linked
        var existingLinks = _xrefAnidbAnilistAnime.GetByAnidbAnimeID(AnimeID);
        if (existingLinks.Count > 0 && !ForceRefresh)
        {
            _logger.LogInformation("Anime already has AniList links: {AnimeID}", AnimeID);
            return;
        }

        // Search for matches
        var results = await _anilistSearchService.SearchForAutoMatch(anime).ConfigureAwait(false);
        if (results.Count == 0)
        {
            _logger.LogInformation("No AniList matches found for anime {AnimeID}", AnimeID);
            return;
        }

        // Link to the first result
        var firstResult = results[0];
        _logger.LogInformation(
            "Linking anime {AnimeName} ({AnimeID}) to AniList anime {AnilistName} ({AnilistID})",
            anime.Title,
            anime.AnimeID,
            firstResult.AnilistAnime.Title,
            firstResult.AnilistAnime.ID
        );

        await _anilistLinkingService.AddAnimeLink(
            anime.AnimeID,
            firstResult.AnilistAnime.ID,
            additiveLink: true,
            matchRating: firstResult.MatchRating
        ).ConfigureAwait(false);

        // Update the AniList anime metadata
        await _anilistMetadataService.ScheduleUpdateOfAnime(new()
        {
            AnimeId = firstResult.AnilistAnime.ID,
            ForceRefresh = ForceRefresh,
            DownloadImages = true,
        }).ConfigureAwait(false);
    }

    public SearchAnilistForMatchJob(
        AnilistLinkingService anilistLinkingService,
        AnilistMetadataService anilistMetadataService,
        AnilistSearchService anilistSearchService,
        AniDB_AnimeRepository anidbAnime,
        CrossRef_AniDB_Anilist_AnimeRepository xrefAnidbAnilistAnime
    )
    {
        _anilistLinkingService = anilistLinkingService;
        _anilistMetadataService = anilistMetadataService;
        _anilistSearchService = anilistSearchService;
        _anidbAnime = anidbAnime;
        _xrefAnidbAnilistAnime = xrefAnidbAnilistAnime;
    }

    protected SearchAnilistForMatchJob() { }
}
