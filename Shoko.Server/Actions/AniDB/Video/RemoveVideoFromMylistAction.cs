using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Dispose of the AniDB MyList entries covering the file, applying the
///   configured delete type — which may mark the entries rather than remove
///   them outright.
/// </summary>
public sealed class RemoveVideoFromMylistAction(IMylistService mylistService) : VideoAction
{
    public override string Name => "Remove from Mylist";

    public override string? Description => "Removes the file from your AniDB Mylist, following your configured delete type.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override bool RequiresConfirmation => true;

    public override string? ConfirmationMessage => "Are you sure you want to remove this file from your AniDB Mylist?";

    public override Task Execute(CancellationToken token = default)
        => mylistService.ScheduleDisposeVideo(Video);
}
