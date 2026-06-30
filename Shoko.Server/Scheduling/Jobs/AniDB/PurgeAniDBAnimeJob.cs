using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Repositories.Cached.AniDB;

#pragma warning disable CS8618
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
        _title = _anidbAnimes.GetByAnimeID(AnimeID)?.MainTitle;
    }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {Anime}", nameof(PurgeAniDBAnimeJob), _title ?? AnimeID.ToString());
        await _anidbService.PurgeAnimeByID(AnimeID, RemoveFromMylist).ConfigureAwait(false);
    }

    private readonly AniDB_AnimeRepository _anidbAnimes;
    public PurgeAniDBAnimeJob(IAnidbService anidbService,
        AniDB_AnimeRepository anidbAnimes
    )
    {
        _anidbService = anidbService;
        _anidbAnimes = anidbAnimes;

    }

    protected PurgeAniDBAnimeJob() { }
}
