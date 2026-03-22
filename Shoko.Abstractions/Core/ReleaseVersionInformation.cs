using System;

namespace Shoko.Abstractions.Core;

/// <summary>
/// Release version information for a component.
/// </summary>
public class ReleaseVersionInformation
{
    /// <summary>
    ///   The version of the component.
    /// </summary>
    public required Version Version { get; set; }

    /// <summary>
    /// A short description about the release of the component.
    /// </summary>
    public required string? Description { get; set; }

    /// <summary>
    ///   The source revision of the component.
    /// </summary>
    public required string SourceRevision { get; set; }

    /// <summary>
    ///   The release tag tied to the source revision.
    /// </summary>
    public required string ReleaseTag { get; set; }

    /// <summary>
    /// The release channel of the component.
    /// </summary>
    public required ReleaseChannel Channel { get; set; }

    /// <summary>
    /// The date and time the component was released.
    /// </summary>
    public required DateTime ReleasedAt { get; set; }
}
