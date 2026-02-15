
using System;

namespace Shoko.Abstractions.Release;

/// <summary>
/// Release group.
/// </summary>
public interface IReleaseGroup : IEquatable<IReleaseGroup>
{
    /// <summary>
    /// The id of the release group.
    /// </summary>
    string ID { get; }

    /// <summary>
    /// The name of the release group, if available.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The short name of the release group, if available.
    /// </summary>
    string ShortName { get; }

    /// <summary>
    /// The source of the release group.
    /// </summary>
    string Source { get; }
}
