
namespace Shoko.Abstractions.Video.Enums;

/// <summary>
/// How files are selected when multiple versions of the same episode exist.
/// </summary>
public enum ReleaseVersionStrategy
{
    /// <summary>
    /// Use the best available version for every episode, regardless of whether
    /// all episodes share the same version.
    /// </summary>
    BestAvailable,

    /// <summary>
    /// Only use files of a single, consistent version across all episodes.
    /// </summary>
    Consistent,
}
