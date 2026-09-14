using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.Anilist;
using Shoko.Server.Repositories.Cached.Anilist;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Anilist;

[DatabaseRequired]
[LimitConcurrency(1, 12)]
[JobKeyGroup(JobKeyGroup.Anilist)]
public class PurgeAnilistAnimeJob : BaseJob
{
    private readonly AnilistMetadataService _anilistService;

    private readonly Anilist_AnimeRepository _anilistAnime;

    public virtual int AnilistAnimeID { get; set; }

    public virtual string? AnimeTitle { get; set; }

    public override void PostInit()
    {
        AnimeTitle ??= _anilistAnime.GetByAnilistAnimeID(AnilistAnimeID)?.PreferredTitle;
    }

    public override string TypeName => "Purge AniList Anime";

    public override string Title => "Purging AniList Anime";

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
        _logger.LogInformation("Processing PurgeAnilistAnimeJob: {AnilistAnimeId}", AnilistAnimeID);
        await _anilistService.PurgeAnime(AnilistAnimeID).ConfigureAwait(false);
    }

    public PurgeAnilistAnimeJob(AnilistMetadataService anilistService, Anilist_AnimeRepository anilistAnime)
    {
        _anilistService = anilistService;
        _anilistAnime = anilistAnime;
    }

    protected PurgeAnilistAnimeJob() { }
}
