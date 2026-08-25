using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   Everything a MyList sync worked out that it should do, and when it worked
///   it out. A plan-only run returns one having done none of it, so it can be shown
///   to a user, narrowed, and handed back to be applied.
/// </summary>
public record MylistSyncPlan
{
    /// <summary>
    ///   The steps to take, in the order the sync arrived at them.
    /// </summary>
    public required IReadOnlyList<MylistSyncAction> Actions { get; init; }

    /// <summary>
    ///   When the plan was worked out, in UTC.
    ///
    ///   Informational: nothing refuses a plan for being old, and applying an
    ///   old one is safe, since a step carries no values — every one is taken
    ///   from current state when the step runs. It is here so a caller can show
    ///   a plan's age, and so the apply log records how old the applied one was.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}
