using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Rescan all files for the group, re-running release matching.
/// </summary>
public sealed class RescanGroupFilesAction(IVideoReleaseService releaseService, IActorContext actorContext) : GroupAction, IVisibleSeriesGroupAction
{
    public override string Name => "Rescan Files";

    public override string? Description => "Rescans every file associated with the group.";

    public override ActionCategory Category => ActionCategory.Import;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(releaseService.AutoMatchEnabled
            ? null
            : new ActionValidationResult("Release auto-matching is currently disabled."));

    public override async Task Execute(CancellationToken token = default)
    {
        var files = Group.GetVisibleSeries(actorContext)
            .SelectMany(s => s.VideoLocals)
            .DistinctBy(v => v.VideoLocalID)
            .ToList();

        foreach (var file in files)
            await releaseService.ScheduleFindReleaseForVideo(file, force: true);
    }
}
