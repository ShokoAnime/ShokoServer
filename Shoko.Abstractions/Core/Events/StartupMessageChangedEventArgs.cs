using System;
using Shoko.Abstractions.Core.Services;

namespace Shoko.Abstractions.Core.Events;

/// <summary>
///   Event arguments for the <see cref="ISystemService.StartupMessageChanged"/> event.
/// </summary>
public class StartupMessageChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The current message that is displayed to the user during startup.
    /// </summary>
    public required string Message { get; init; }
}
