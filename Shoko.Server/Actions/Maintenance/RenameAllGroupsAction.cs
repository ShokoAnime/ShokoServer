using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Works out every series' title again, which the groups without a custom
///   name follow, using the current language preferences.
/// </summary>
public sealed class RenameAllGroupsAction(IShokoGroupManager groupManager) : IScheduledAction
{
    public string Name => "Rename All Groups";

    public string? Description => "Rename all groups without a custom name using the current language preferences.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        groupManager.RenameAllGroups();
        return Task.CompletedTask;
    }
}
