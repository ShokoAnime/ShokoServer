using System;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Base class for repository sync event arguments.
/// </summary>
public class RepositorySyncEventArgs : EventArgs
{
    /// <summary>
    ///   The repository to synchronize with.
    /// </summary>
    public required PackageRepositoryInfo Repository { get; init; }

    /// <summary>
    ///   When the repository sync operation started.
    /// </summary>
    public required DateTime StartedAt { get; init; }
}
