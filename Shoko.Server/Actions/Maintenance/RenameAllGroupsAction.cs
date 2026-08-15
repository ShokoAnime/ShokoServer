using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Rename all groups that do not have a custom name set, using the current
///   language preferences.
/// </summary>
public sealed class RenameAllGroupsAction(IShokoGroupManager groupManager) : IExecutableAction
{
    public string Name => "Rename All Groups";

    public string? Description => "Rename all groups without a custom name using the current language preferences.";

    public ActionCategory Category => ActionCategory.Maintenance;

    // Matches today: [Authorize("admin")] on the legacy RenameAllGroups endpoint.
    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
    {
        groupManager.RenameAllGroups();
        return Task.CompletedTask;
    }
}
