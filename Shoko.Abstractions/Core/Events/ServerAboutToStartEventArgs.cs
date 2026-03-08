using System;

namespace Shoko.Abstractions.Core.Events;

/// <summary>
///   Dispatched when core system services are ready to be used.
/// </summary>
public class ServerAboutToStartEventArgs : EventArgs
{
    /// <summary>
    ///   The service provider that can be used to resolve services if needed.
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }
}
