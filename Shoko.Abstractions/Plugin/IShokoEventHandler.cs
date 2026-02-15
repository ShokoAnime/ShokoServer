using System;

namespace Shoko.Abstractions.Plugin;

/// <summary>
/// Interface for Shoko event handlers.
/// </summary>
public interface IShokoEventHandler
{
    /// <summary>
    /// Fired when the the server has fully started and all services are usable.
    /// </summary>
    event EventHandler Started;

    /// <summary>
    /// Fired when the the server is shutting down.
    /// </summary>
    event EventHandler Shutdown;
}
