using System;

namespace Shoko.Abstractions.Core;

/// <summary>
///   Information about the core version of the server.
/// </summary>
public record VersionInformation
{
    /// <summary>
    ///   The version of the currently running server.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    ///   The source revision of the currently running server. This may be null
    ///   if the server was not built with the source revision information.
    /// </summary>
    public required string? SourceRevision { get; init; }

    /// <summary>
    ///   The version tag of the currently running server. This may be null if
    ///   the server was not built with the version tag information.
    /// </summary>
    public required string? Tag { get; init; }

    /// <summary>
    /// The release channel of the currently running server.
    /// </summary>
    public required ReleaseChannel Channel { get; init; }

    /// <summary>
    /// The date and time the server was built, if available.
    /// </summary>
    public required DateTime? ReleasedAt { get; init; }
}
