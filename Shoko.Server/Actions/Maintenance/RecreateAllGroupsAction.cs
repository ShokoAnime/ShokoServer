using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Tasks;

namespace Shoko.Server.Actions;

/// <summary>
///   Delete all existing groups and recreate them from scratch based on
///   current settings.
/// </summary>
public sealed class RecreateAllGroupsAction(AnimeGroupCreator groupCreator) : IExecutableAction
{
    public string Name => "Recreate All Groups";

    public string? Description => "Delete all groups and recreate them from scratch based on current settings.";

    public ActionCategory Category => ActionCategory.Maintenance;

    // Matches today: [Authorize("admin")] on the legacy RecreateAllGroups endpoint.
    public ActionPermission Permission => ActionPermission.Admin;

    public bool RequiresConfirmation => true;

    public Task Execute(CancellationToken token = default)
        => groupCreator.RecreateAllGroups();
}
