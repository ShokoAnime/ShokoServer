using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh the airing schedules for the series with every enabled provider.
/// </summary>
public sealed class RefreshAiringScheduleSeriesAction(IAiringScheduleService airingScheduleService) : SeriesAction
{
    public override string Name => "Refresh Airing Schedule";

    public override string? Description => "Refreshes the airing schedules for the series with every enabled provider.";

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(airingScheduleService.GetAvailableProviders(onlyEnabled: true).Any()
            ? null
            : new ActionValidationResult("No airing schedule provider is enabled."));

    public override Task Execute(CancellationToken token = default)
        => airingScheduleService.ScheduleRefresh(Series);
}
