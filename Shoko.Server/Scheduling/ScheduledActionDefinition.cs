using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Scheduling;

/// <summary>
/// A registered scheduled action: what it says of itself, and the key of the
/// queue job a run uses.
/// </summary>
/// <param name="ID">
/// The UUIDv5 of the type's full name, namespaced by the owning plugin's ID.
/// </param>
/// <param name="Name">The display name.</param>
/// <param name="Description">The description, if any.</param>
/// <param name="Category">The category.</param>
/// <param name="CategoryName">The category's display name.</param>
/// <param name="RequiresConfirmation">Whether a client asks before a run by hand.</param>
/// <param name="ConfirmationMessage">The question to ask, if any.</param>
/// <param name="PluginID">The ID of the owning plugin.</param>
/// <param name="DefaultTriggers">The triggers it declares for itself.</param>
/// <param name="MinimumInterval">
/// The shortest time after a run before it runs on its own again: what it
/// declares, or <see cref="ActionTrigger.MinimumInterval"/>.
/// </param>
/// <param name="ScheduleCountsManualRuns">Whether a run by hand counts for its schedule.</param>
/// <param name="JobKey">The key of the queue job a run uses.</param>
public sealed record ScheduledActionDefinition(
    Guid ID,
    string Name,
    string? Description,
    ActionCategory Category,
    string CategoryName,
    bool RequiresConfirmation,
    string? ConfirmationMessage,
    Guid PluginID,
    IReadOnlyList<ActionTrigger> DefaultTriggers,
    TimeSpan MinimumInterval,
    bool ScheduleCountsManualRuns,
    string JobKey
);
