using System;
using Shoko.Abstractions.Extensions;

namespace Shoko.Abstractions.Core;

/// <summary>
///   Version information about a component.
/// </summary>
public record VersionInformation : IComparable<VersionInformation>
{
    /// <summary>
    ///   The version of the component.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    ///   The .NET runtime identifier (e.g. <c>"win-64"</c>, <c>"linux-x64"</c>, etc.) the component was built for.
    /// </summary>
    public required string RuntimeIdentifier { get; init; }

    /// <summary>
    ///   The version of the plugin abstractions the component was built against.
    /// </summary>
    public required Version AbstractionVersion { get; set; }

    /// <summary>
    ///   The source revision of the component, if available.
    /// </summary>
    public required string? SourceRevision { get; init; }

    /// <summary>
    ///   The release tag tied to the source revision, if available.
    /// </summary>
    public required string? ReleaseTag { get; init; }

    /// <summary>
    /// The release channel of the component.
    /// </summary>
    public required ReleaseChannel Channel { get; init; }

    /// <summary>
    /// The date and time the component was released.
    /// </summary>
    public required DateTime ReleasedAt { get; init; }

    /// <inheritdoc/>
    public int CompareTo(VersionInformation? other)
    {
        if (other is null)
            return 1;

        var result = Version.CompareTo(other.Version);
        if (result != 0)
            return result;

        result = RuntimeIdentifier.CompareTo(other.RuntimeIdentifier);
        if (result != 0)
            return result;

        result = AbstractionVersion.CompareTo(other.AbstractionVersion);
        if (result != 0)
            return result;

        result = StringComparer.InvariantCulture.Compare(SourceRevision, other.SourceRevision);
        if (result != 0)
            return result;

        result = StringComparer.CurrentCulture.Compare(ReleaseTag, other.ReleaseTag);
        if (result != 0)
            return result;

        result = Channel.CompareTo(other.Channel);
        if (result != 0)
            return result;

        return ReleasedAt.CompareTo(other.ReleasedAt);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var version = $"v{Version.ToSemanticVersioningString()}, Abstraction: {AbstractionVersion.ToSemanticVersioningString()}, Channel: {Channel}";
        if (SourceRevision is { Length: > 0 })
            version += $", Source Revision: {SourceRevision}";
        if (ReleaseTag is { Length: > 0 })
            version += $", Release Tag: {ReleaseTag}";
        if (RuntimeIdentifier is not "any")
            version += $", Runtime: {RuntimeIdentifier}";
        version += $", Released At: {ReleasedAt:yyyy-MM-dd}";
        return version;
    }
}
