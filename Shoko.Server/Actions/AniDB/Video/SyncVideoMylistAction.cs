using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Reconcile the AniDB MyList entries covering the file against the local
///   state — adding what is missing and syncing watched and storage states in
///   both directions, as the full sync would.
/// </summary>
public sealed class SyncVideoMylistAction(IMylistService mylistService) : VideoAction
{
    public override string Name => "Sync Mylist";

    public override string? Description => "Reconciles your AniDB Mylist with the local state for the file.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task Execute(CancellationToken token = default)
        => mylistService.ScheduleSync([Video]);
}
