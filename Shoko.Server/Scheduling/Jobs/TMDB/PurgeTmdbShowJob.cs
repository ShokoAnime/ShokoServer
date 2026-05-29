using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(1, 12)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class PurgeTmdbShowJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    public virtual int TmdbShowID { get; set; }

    public virtual string? ShowTitle { get; set; }

    public override void PostInit()
    {
        ShowTitle ??= RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID)?.EnglishTitle;
    }

    public override string TypeName => "Purge TMDB Show";

    public override string Title => "Purging TMDB Show";

    public override Dictionary<string, object> Details => string.IsNullOrEmpty(ShowTitle)
        ? new()
        {
            {"ShowID", TmdbShowID},
        }
        : new()
        {
            {"Show", ShowTitle},
            {"ShowID", TmdbShowID},
        };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing PurgeTmdbShowJob: {TmdbShowId}", TmdbShowID);
        await _tmdbService.PurgeShow(TmdbShowID).ConfigureAwait(false);
    }

    public PurgeTmdbShowJob(TmdbMetadataService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    protected PurgeTmdbShowJob() { }
}
