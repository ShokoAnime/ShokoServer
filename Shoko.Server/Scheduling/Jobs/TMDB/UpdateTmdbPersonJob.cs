using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Direct.TMDB;

#pragma warning disable CS8618
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[TmdbApiRateLimited]
[LimitConcurrency(1, 12)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public class UpdateTmdbPersonJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    public virtual int TmdbPersonID { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public virtual bool DownloadImages { get; set; }

    /// <summary>
    /// Loop-prevention hint: the show that triggered this job. When set, <see cref="TmdbMetadataService.PurgePersonInternal"/>
    /// skips re-scheduling an update for this show, preventing a cascade when the person 404s.
    /// Not a true parent-context ID — the person update is independent of any specific show.
    /// </summary>
    public virtual int? TmdbShowID { get; set; }

    /// <summary>
    /// Loop-prevention hint: the movie that triggered this job. When set, <see cref="TmdbMetadataService.PurgePersonInternal"/>
    /// skips re-scheduling an update for this movie, preventing a cascade when the person 404s.
    /// Not a true parent-context ID — the person update is independent of any specific movie.
    /// </summary>
    public virtual int? TmdbMovieID { get; set; }

    public virtual string? PersonName { get; set; }

    public override void PostInit()
    {
        PersonName ??= _tmdbPeople.GetByTmdbPersonID(TmdbPersonID)?.EnglishName;
    }

    public override string TypeName => "Update TMDB Person";

    public override string Title => "Updating TMDB Person";

    public override Dictionary<string, object> Details => string.IsNullOrEmpty(PersonName)
        ? new() { { "PersonID", TmdbPersonID } }
        : new() { { "Person", PersonName }, { "PersonID", TmdbPersonID } };

    public override async Task Execute()
    {
        _logger.LogInformation("Processing UpdateTmdbPersonJob: {TmdbPersonId}", TmdbPersonID);
        await _tmdbService.UpdatePerson(TmdbPersonID, ForceRefresh, DownloadImages, currentShowId: TmdbShowID, currentMovieId: TmdbMovieID).ConfigureAwait(false);
    }

    private readonly TMDB_PersonRepository _tmdbPeople;

    public UpdateTmdbPersonJob(TmdbMetadataService tmdbService, TMDB_PersonRepository tmdbPeople)
    {
        _tmdbService = tmdbService;
        _tmdbPeople = tmdbPeople;
    }

    protected UpdateTmdbPersonJob() { }
}
