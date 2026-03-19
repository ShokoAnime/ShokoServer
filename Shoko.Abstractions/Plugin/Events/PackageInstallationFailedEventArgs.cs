using System;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Event dispatched when package installation fails.
/// </summary>
public sealed class PackageInstallationFailedEventArgs : PackageInstallationEventArgs
{
    /// <summary>
    /// Error message describing the failure.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    ///   Original exception if available.
    /// </summary>
    public required Exception? Exception { get; init; }

    /// <summary>
    ///   When the package installation operation failed.
    /// </summary>
    public required DateTime FailedAt { get; init; }
}
