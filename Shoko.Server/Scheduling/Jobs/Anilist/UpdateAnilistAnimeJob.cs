using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Providers.Anilist;
using Shoko.Server.Repositories.Cached.Anilist;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Anilist;

[DatabaseRequired]
[AnilistApiRateLimited]
[LongRunning]
[LimitConcurrency(4, 16)]
[JobKeyGroup(JobKeyGroup.Anilist)]
public class UpdateAnilistAnimeJob : BaseJob
{
    private readonly AnilistMetadataService _anilistService;

    private readonly Anilist_AnimeRepository _anilistAnime;

    public virtual int AnilistAnimeID { get; set; }

    public virtual bool DownloadImages { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public virtual bool QuickRefresh { get; set; }

    public virtual bool? DownloadCharactersAndStaff { get; set; }

    public virtual string? AnimeTitle { get; set; }

    public override void PostInit()
    {
        AnimeTitle ??= _anilistAnime.GetByAnilistAnimeID(AnilistAnimeID)?.PreferredTitle;
    }

    public override string TypeName => string.IsNullOrEmpty(AnimeTitle)
        ? "Download AniList Anime"
        : "Update AniList Anime";

    public override string Title => string.IsNullOrEmpty(AnimeTitle)
        ? "Downloading AniList Anime"
        : "Updating AniList Anime";

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
        _logger.LogInformation("Processing UpdateAnilistAnimeJob: {AnilistAnimeId}", AnilistAnimeID);
        await _anilistService.UpdateAnime(new()
        {
            AnimeId = AnilistAnimeID,
            ForceRefresh = ForceRefresh,
            QuickRefresh = QuickRefresh,
            DownloadImages = DownloadImages,
            DownloadCharactersAndStaff = DownloadCharactersAndStaff,
        }).ConfigureAwait(false);
    }

    public UpdateAnilistAnimeJob(AnilistMetadataService anilistService, Anilist_AnimeRepository anilistAnime)
    {
        _anilistService = anilistService;
        _anilistAnime = anilistAnime;
    }

    protected UpdateAnilistAnimeJob() { }
}
