using System;
using Shoko.Abstractions.Core.Services;

namespace Shoko.Abstractions.Core.Events;

/// <summary>
///   Event arguments for the <see cref="ISystemService.DatabaseBlockedChanged"/> event.
/// </summary>
public class DatabaseBlockedChangedEventArgs : EventArgs
{
    /// <summary>
    ///   Indicates whether the database is currently blocked.
    /// </summary>
    public required bool IsBlocked { get; init; }
}
