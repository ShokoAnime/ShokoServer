using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Scheduling.Watchdog;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Airing;

/// <summary>
/// One chunk of one provider's sweep. It stands in for every
/// <see cref="ISweepingAiringScheduleProvider"/>, the way
/// <see cref="RefreshAiringScheduleJob"/> does for a refresh, so a provider
/// implements only the walk.
/// </summary>
/// <remarks>
/// The job is keyed by provider, so a chunk queued while the same provider's
/// chunk is waiting or running is a no-op. It holds a worker for the chunk's
/// deadline at the very most, and the service queues the next chunk itself when
/// the provider says there is more to do. The deadline can be longer than the
/// queue watchdog's own timeout, so
/// <see cref="AiringScheduleSweepWatchdogThreshold"/>
/// tells the watchdog what to watch this job against: a chunk that runs its
/// whole budget is expected, one that runs half as long again is a provider
/// ignoring the token it was handed, and is reported.
/// </remarks>
[DatabaseRequired]
[NetworkRequired]
[LimitConcurrency(1, 4)]
[JobKeyGroup(JobKeyGroup.Airing)]
public class SweepAiringScheduleProviderJob(IAiringScheduleService airingScheduleService, IJobCancellationAccessor cancellationAccessor) : BaseJob
{
    private readonly AiringScheduleService _airingScheduleService = (AiringScheduleService)airingScheduleService;

    private AiringScheduleProviderInfo? _providerInfo;

    /// <summary>
    /// The provider to sweep with.
    /// </summary>
    [JobKeyMember]
    public Guid ProviderID { get; set; }

    /// <inheritdoc/>
    public override string TypeName => "Sweep Airing Schedules From Provider";

    /// <inheritdoc/>
    public override string Title => "Sweeping Airing Schedules From Provider";

    /// <inheritdoc/>
    public override Dictionary<string, object> Details
        => new()
        {
            ["Provider"] = _providerInfo?.Name ?? ProviderID.ToString(),
        };

    /// <inheritdoc/>
    public override void PostInit()
        => _providerInfo = _airingScheduleService.GetProviderInfo(ProviderID);

    /// <inheritdoc/>
    public override async Task Execute()
    {
        // The worker's own token, which the service links this chunk's deadline
        // to, so the provider sees one token for both.
        await _airingScheduleService.ExecuteSweepAsync(ProviderID, cancellationAccessor.Token);
    }
}
