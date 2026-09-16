using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Airing;

/// <summary>
/// Asks one airing schedule provider to refresh what it knows about one
/// entity. It stands in for every provider, the way
/// <c>ProcessReleaseProviderJob</c> does for release providers without a job
/// of their own, so a provider implements only the work.
/// </summary>
/// <remarks>
/// The job is keyed by provider and entity, so a hint that arrives while the
/// same refresh is waiting or running is a no-op, and ten refresh clicks
/// collapse into one. One shared job type can't vary its attributes per
/// provider, so a provider's own
/// <see cref="IAiringScheduleProvider.MaxConcurrentRefreshes"/> is honoured by
/// the service rather than here.
/// </remarks>
[NetworkRequired]
[LimitConcurrency(2, 8)]
[JobKeyGroup(JobKeyGroup.Airing)]
public class RefreshAiringScheduleJob(IAiringScheduleService airingScheduleService) : BaseJob
{
    private readonly AiringScheduleService _airingScheduleService = (AiringScheduleService)airingScheduleService;

    private AiringScheduleProviderInfo? _providerInfo;

    /// <summary>
    /// The provider to refresh with.
    /// </summary>
    public Guid ProviderID { get; set; }

    /// <summary>
    /// The source of the entity to refresh.
    /// </summary>
    public DataSource EntitySource { get; set; }

    /// <summary>
    /// The kind of entity to refresh.
    /// </summary>
    public DataEntityType EntityType { get; set; }

    /// <summary>
    /// The ID of the entity within its source.
    /// </summary>
    public string EntityID { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string TypeName => "Refresh Airing Schedules From Provider";

    /// <inheritdoc/>
    public override string Title => "Refreshing Airing Schedules From Provider";

    /// <inheritdoc/>
    public override Dictionary<string, object> Details
        => new()
        {
            ["Provider"] = _providerInfo?.Name ?? ProviderID.ToString(),
            ["Entity"] = $"{EntitySource} {EntityType} {EntityID}",
        };

    /// <inheritdoc/>
    public override void PostInit()
        => _providerInfo = _airingScheduleService.GetProviderInfo(ProviderID);

    /// <inheritdoc/>
    public override async Task Execute()
    {
        _providerInfo ??= _airingScheduleService.GetProviderInfo(ProviderID);
        _logger.LogInformation(
            "Refreshing airing schedules for {EntityType} {EntitySource}:{EntityID} with provider {ProviderName}.",
            EntityType,
            EntitySource,
            EntityID,
            _providerInfo?.Name ?? ProviderID.ToString()
        );

        await _airingScheduleService.ExecuteRefreshAsync(ProviderID, EntitySource, EntityType, EntityID, CancellationToken.None);
    }
}
