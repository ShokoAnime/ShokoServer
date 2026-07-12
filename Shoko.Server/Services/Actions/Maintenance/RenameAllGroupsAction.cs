using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Services.Actions.Maintenance;

/// <summary>
///   Rename all groups that do not have a custom name set, using the current
///   language preferences.
/// </summary>
internal sealed class RenameAllGroupsAction(IShokoGroupManager groupManager) : IExecutableGlobalAction
{
    public string Name => "Rename All Groups";
    public string? Description => "Rename all groups without a custom name using the current language preferences.";
    public ActionCategory Category => ActionCategory.Maintenance;

    public Task Execute(CancellationToken cancellationToken = default)
    {
        groupManager.RenameAllGroups();
        return Task.CompletedTask;
    }
}
