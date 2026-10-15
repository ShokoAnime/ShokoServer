using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity.Suspensions.Attributes;
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
/// One chunk of one provider's sweep. The core runs it for every
/// <see cref="ISweepingAiringScheduleProvider"/>, so a provider implements
/// only the walk.
/// </summary>
/// <remarks>
/// One job type per provider, so a suspended provider holds back only its own
/// chunks, and a chunk queued while the provider's chunk is waiting or running
/// is a no-op. The deadline can be longer than the queue watchdog's own
/// timeout, so <see cref="AiringScheduleSweepWatchdogThreshold"/> tells the
/// watchdog what to watch it against.
/// </remarks>
/// <typeparam name="TProvider">The provider to sweep with.</typeparam>
[DatabaseRequired]
[NetworkRequired]
[ProviderJob]
[LimitConcurrency(1, 4)]
[JobKeyGroup(JobKeyGroup.Airing)]
public class SweepAiringScheduleProviderJob<TProvider>(
    IAiringScheduleService airingScheduleService,
    IJobCancellationAccessor cancellationAccessor
) : BaseJob where TProvider : class, ISweepingAiringScheduleProvider
{
    #region Properties

    private readonly AiringScheduleService _airingScheduleService = (AiringScheduleService)airingScheduleService;

    private AiringScheduleProviderInfo? _providerInfo;

    /// <inheritdoc/>
    public override string TypeName => "Sweep Airing Schedules From Provider";

    /// <inheritdoc/>
    public override string Title => "Sweeping Airing Schedules From Provider";

    /// <inheritdoc/>
    public override Dictionary<string, object> Details
        => new()
        {
            ["Provider"] = _providerInfo?.Name ?? typeof(TProvider).Name,
        };

    #endregion

    #region Execution

    /// <inheritdoc/>
    public override void PostInit()
        => _providerInfo = _airingScheduleService.GetProviderInfo(typeof(TProvider));

    /// <inheritdoc/>
    public override async Task Execute()
    {
        _providerInfo ??= _airingScheduleService.GetProviderInfo(typeof(TProvider));
        if (_providerInfo is null)
        {
            _logger.LogWarning("Skipped a sweep for the unregistered provider {ProviderType}.", typeof(TProvider).FullName);
            return;
        }

        // The worker's own token, which the service links this chunk's deadline
        // to, so the provider sees one token for both.
        await _airingScheduleService.ExecuteSweepAsync(_providerInfo.ID, cancellationAccessor.Token);
    }

    #endregion
}
