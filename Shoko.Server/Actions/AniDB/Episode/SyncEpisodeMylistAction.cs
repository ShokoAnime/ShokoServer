using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Reconcile the AniDB MyList entries covering the episode against the local
///   state — its generic entry as well as the file entries of any files it has,
///   adding what is missing and syncing watched and storage states in both
///   directions, as the full sync would. Works even with no files, which is the
///   case the generic entry exists for.
/// </summary>
public sealed class SyncEpisodeMylistAction(IMylistService mylistService) : EpisodeAction
{
    public override string Name => "Sync MyList";

    public override string? Description => "Reconciles your AniDB MyList with the local state for the episode, with or without files.";

    public override ActionCategory Category => ActionCategory.AniDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task Execute(CancellationToken token = default)
        => mylistService.ScheduleSync([Episode]);
}
