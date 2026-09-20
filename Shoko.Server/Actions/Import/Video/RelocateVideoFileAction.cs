using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Actions;

/// <summary>
///   Relocate the file.
/// </summary>
public sealed class RelocateVideoFileAction(IVideoRelocationService relocationService) : VideoAction
{
    public override string Name => "Relocate File";

    public override string? Description => "Renames and/or moves the file.";

    public override ActionCategory Category => ActionCategory.Import;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task Execute(CancellationToken token = default)
        => relocationService.ScheduleAutoRelocationForVideo((VideoLocal)Video);
}
