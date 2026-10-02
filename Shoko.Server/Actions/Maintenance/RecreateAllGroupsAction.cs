using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Delete all existing groups and recreate them from scratch based on
///   current settings.
/// </summary>
public sealed class RecreateAllGroupsAction(IShokoGroupManager groupManager) : IScheduledAction
{
    public string Name => "Recreate All Groups";

    public string? Description => "Delete all groups and recreate them from scratch based on current settings.";

    public ActionCategory Category => ActionCategory.Maintenance;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to rebuild all groups from scratch?";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => groupManager.RecreateAllGroups(progress, token);
}
