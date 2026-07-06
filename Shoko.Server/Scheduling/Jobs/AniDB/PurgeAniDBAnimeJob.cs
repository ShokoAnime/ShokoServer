using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class PurgeAniDBAnimeJob(IAnidbService anidbService, AniDB_AnimeRepository anidbAnimeRepository) : BaseJob
{
    private string? _title;

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
        _title = anidbAnimeRepository.GetByAnimeID(AnimeID)?.MainTitle;
    }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job} for {Anime}", nameof(PurgeAniDBAnimeJob), _title ?? AnimeID.ToString());
        await anidbService.PurgeAnimeByID(AnimeID, RemoveFromMylist).ConfigureAwait(false);
    }
}
