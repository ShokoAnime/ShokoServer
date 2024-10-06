
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Release group.
/// </summary>
public interface IReleaseGroup : IMetadata<int>
{
    /// <summary>
    /// The name of the release group, if available.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// The short name of the release group, if available.
    /// </summary>
    string? ShortName { get; }
}
