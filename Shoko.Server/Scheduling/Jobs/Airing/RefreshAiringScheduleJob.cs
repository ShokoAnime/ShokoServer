using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity.Suspensions.Attributes;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Airing;

/// <summary>
/// Asks one airing schedule provider to refresh what it knows about one
/// entity. The core runs it for every provider, so a provider implements only
/// the work.
/// </summary>
/// <remarks>
/// One job type per provider, so a suspended provider holds back only its own
/// refreshes, and one job per entity, so ten refresh clicks collapse into one.
/// The provider's own <see cref="IAiringScheduleProvider.MaxConcurrentRefreshes"/>
/// is honoured by the service.
/// </remarks>
/// <typeparam name="TProvider">The provider to refresh with.</typeparam>
[DatabaseRequired]
[NetworkRequired]
[ProviderJob]
[LimitConcurrency(2, 8)]
[JobKeyGroup(JobKeyGroup.Airing)]
public class RefreshAiringScheduleJob<TProvider>(IAiringScheduleService airingScheduleService) : BaseJob, IRefreshAiringScheduleJob
    where TProvider : class, IAiringScheduleProvider
{
    #region Properties

    private readonly AiringScheduleService _airingScheduleService = (AiringScheduleService)airingScheduleService;

    private AiringScheduleProviderInfo? _providerInfo;

    /// <inheritdoc/>
    [JobKeyMember]
    public MetadataSource EntitySource { get; set; } = null!;

    /// <inheritdoc/>
    [JobKeyMember]
    public MetadataEntityType EntityType { get; set; } = null!;

    /// <inheritdoc/>
    [JobKeyMember]
    public string EntityID { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string TypeName => "Refresh Airing Schedules From Provider";

    /// <inheritdoc/>
    public override string Title => "Refreshing Airing Schedules From Provider";

    /// <inheritdoc/>
    public override Dictionary<string, object> Details
        => new()
        {
            ["Provider"] = _providerInfo?.Name ?? typeof(TProvider).Name,
            ["Source"] = EntitySource?.Name ?? string.Empty,
            ["Kind"] = EntityType?.Name ?? string.Empty,
            ["ID"] = EntityID,
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
            _logger.LogWarning("Skipped a refresh for the unregistered provider {ProviderType}.", typeof(TProvider).FullName);
            return;
        }

        _logger.LogInformation(
            "Refreshing airing schedules for {EntityType} {EntitySource}:{EntityID} with provider {ProviderName}.",
            EntityType,
            EntitySource,
            EntityID,
            _providerInfo.Name
        );

        await _airingScheduleService.ExecuteRefreshAsync(_providerInfo.ID, EntitySource, EntityType, EntityID, CancellationToken.None);
    }

    #endregion
}
