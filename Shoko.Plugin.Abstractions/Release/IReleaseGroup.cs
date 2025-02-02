
namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Release group.
/// </summary>
public interface IReleaseGroup
{
    /// <summary>
    /// The id of the release group.
    /// </summary>
    string ID { get; }

    /// <summary>
    /// The provider id of the release group.
    /// </summary>
    string ProviderID { get; }

    /// <summary>
    /// The name of the release group, if available.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// The short name of the release group, if available.
    /// </summary>
    string? ShortName { get; }
}
