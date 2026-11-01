using System;
using Shoko.Abstractions.Core.Services;

namespace Shoko.Abstractions.Core.Events;

/// <summary>
///   Event arguments for the <see cref="ISystemService.StartupMessageChanged"/> event.
/// </summary>
public class StartupMessageChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The message, with when it was shown, as kept in
    ///   <see cref="ISystemService.StartupMessages"/>.
    /// </summary>
    public required StartupMessageEntry Entry { get; init; }

    /// <summary>
    ///   The current message that is displayed to the user during startup.
    /// </summary>
    public string Message => Entry.Message;
}
