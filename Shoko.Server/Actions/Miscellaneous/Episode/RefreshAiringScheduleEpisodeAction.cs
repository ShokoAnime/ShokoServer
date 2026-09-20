using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh the airing schedules covering the episode with every enabled
///   provider.
/// </summary>
public sealed class RefreshAiringScheduleEpisodeAction(IAiringScheduleService airingScheduleService) : EpisodeAction
{
    public override string Name => "Refresh Airing Schedule";

    public override string? Description => "Refreshes the airing schedules covering the episode with every enabled provider.";

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(airingScheduleService.GetAvailableProviders(onlyEnabled: true).Any()
            ? null
            : new ActionValidationResult("No airing schedule provider is enabled."));

    public override Task Execute(CancellationToken token = default)
        => airingScheduleService.ScheduleRefresh(Episode);
}
