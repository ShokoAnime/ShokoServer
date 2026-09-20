using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Rescan the file, re-running release matching.
/// </summary>
public sealed class RescanVideoFileAction(IVideoReleaseService releaseService) : VideoAction
{
    public override string Name => "Rescan File";

    public override string? Description => "Rescans the file, re-running release matching.";

    public override ActionCategory Category => ActionCategory.Import;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(releaseService.AutoMatchEnabled
            ? null
            : new ActionValidationResult("Release auto-matching is currently disabled."));

    public override Task Execute(CancellationToken token = default)
        => releaseService.ScheduleFindReleaseForVideo((VideoLocal)Video, force: true);
}
