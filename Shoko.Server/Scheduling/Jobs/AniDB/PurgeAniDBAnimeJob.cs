using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class PurgeAniDBAnimeJob : BaseJob
{
    private string? _title;
    private readonly IAnidbService _anidbService;

    public int AnimeID { get; set; }

    public bool RemoveFromMylist { get; set; } = true;

    public override string TypeName => "Purge AniDB Anime";

    public override string Title => "Purging AniDB Anime";

    public override Dictionary<string, object> Details => string.IsNullOrEmpty(_title)
        ? new()
        {
            { "AnimeID", AnimeID },
        }
        : new()
        {
            { "Anime", _title },
            { "AnimeID", AnimeID },
        };

    public override void PostInit()
    {
        _title = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID)?.MainTitle;
    }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {Anime}", nameof(PurgeAniDBAnimeJob), _title ?? AnimeID.ToString());
        await _anidbService.PurgeAnimeByID(AnimeID, RemoveFromMylist).ConfigureAwait(false);
    }

    public PurgeAniDBAnimeJob(IAnidbService anidbService)
    {
        _anidbService = anidbService;
    }

    protected PurgeAniDBAnimeJob() { }
}
