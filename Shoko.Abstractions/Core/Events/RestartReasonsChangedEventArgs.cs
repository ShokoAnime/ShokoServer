using System;
using System.Collections.Generic;
using Shoko.Abstractions.Core.Services;

namespace Shoko.Abstractions.Core.Events;

/// <summary>
///   Event arguments for the <see cref="ISystemService.RestartReasonsChanged"/>
///   event, carrying every reason that stands after the change.
/// </summary>
public class RestartReasonsChangedEventArgs : EventArgs
{
    /// <summary>
    ///   Every reason the server needs a restart, after the change. Empty once
    ///   the last one is cleared.
    /// </summary>
    public required IReadOnlyList<RestartReason> Reasons { get; init; }

    /// <summary>
    ///   Indicates that at least one reason stands.
    /// </summary>
    public bool RestartRequired => Reasons.Count > 0;
}
