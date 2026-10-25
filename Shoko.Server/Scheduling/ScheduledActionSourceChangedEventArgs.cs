using System.Collections.Generic;

namespace Shoko.Server.Scheduling;

/// <summary>
/// The scheduled actions a provider change added, updated and removed.
/// </summary>
/// <param name="Added">The actions added.</param>
/// <param name="Updated">The actions whose declarations changed, as they are now.</param>
/// <param name="Removed">The actions removed, as they were.</param>
public sealed record ScheduledActionSourceChangedEventArgs(
    IReadOnlyList<ScheduledActionDefinition> Added,
    IReadOnlyList<ScheduledActionDefinition> Updated,
    IReadOnlyList<ScheduledActionDefinition> Removed
);
