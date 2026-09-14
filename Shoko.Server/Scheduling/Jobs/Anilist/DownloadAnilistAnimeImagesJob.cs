using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.Anilist;
using Shoko.Server.Repositories.Cached.Anilist;
using Shoko.Server.Scheduling.Acquisition.Attributes;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Anilist;

[DatabaseRequired]
[AnilistApiRateLimited]
[LongRunning]
[LimitConcurrency(1, 16)]
[JobKeyGroup(JobKeyGroup.Anilist)]
public class DownloadAnilistAnimeImagesJob : BaseJob
{
    private readonly AnilistMetadataService _anilistService;

    private readonly Anilist_AnimeRepository _anilistAnime;

    public virtual int AnilistAnimeID { get; set; }

    public virtual bool ForceDownload { get; set; } = true;

    public virtual string? AnimeTitle { get; set; }

    public override void PostInit()
    {
        AnimeTitle ??= _anilistAnime.GetByAnilistAnimeID(AnilistAnimeID)?.PreferredTitle;
    }

    public override string TypeName => "Download Images for AniList Anime";

    public override string Title => "Downloading Images for AniList Anime";

    public override Dictionary<string, object> Details => string.IsNullOrEmpty(AnimeTitle)
        ? new()
        {
            { "AnimeID", AnilistAnimeID },
        }
        : new()
        {
            { "Anime", AnimeTitle },
            { "AnimeID", AnilistAnimeID },
        };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing DownloadAnilistAnimeImagesJob: {AnilistAnimeId}", AnilistAnimeID);
        await _anilistService.DownloadAllAnimeImages(AnilistAnimeID, ForceDownload).ConfigureAwait(false);
    }

    public DownloadAnilistAnimeImagesJob(AnilistMetadataService anilistService, Anilist_AnimeRepository anilistAnime)
    {
        _anilistService = anilistService;
        _anilistAnime = anilistAnime;
    }

    protected DownloadAnilistAnimeImagesJob() { }
}
