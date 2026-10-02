using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Relocate all files for the group.
/// </summary>
public sealed class RelocateGroupFilesAction(IVideoRelocationService relocationService, IActorContext actorContext) : GroupAction, IVisibleSeriesGroupAction
{
    public override string Name => "Relocate Files";

    public override string? Description => "Renames and/or moves every file associated with the group.";

    public override ActionCategory Category => ActionCategory.Import;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        var files = Group.GetVisibleSeries(actorContext)
            .SelectMany(s => s.VideoLocals)
            .DistinctBy(v => v.VideoLocalID)
            .ToList();

        foreach (var file in files)
            await relocationService.ScheduleAutoRelocationForVideo(file);
    }
}
