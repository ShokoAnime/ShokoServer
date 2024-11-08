
namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Release group.
/// </summary>
public interface IReleaseGroup : IMetadata<int>
{
    /// <summary>
    /// The name of the release group.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// The short name of the release group.
    /// </summary>
    string? ShortName { get; }
}
