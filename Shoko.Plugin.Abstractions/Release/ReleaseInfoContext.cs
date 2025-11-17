using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Context for when requesting release information for a video.
/// </summary>
public class ReleaseInfoContext
{
    /// <summary>
    /// The video for which to get release information.
    /// </summary>
    public required IVideo Video { get; init; }

    /// <summary>
    /// Indicates this request is for an automated search.
    /// </summary>
    public required bool IsAutomatic { get; init; }

    /// <summary>
    /// Deconstruct the request into its component parts.
    /// </summary>
    /// <param name="video">The video for which to get release information.</param>
    /// <param name="isAutomatic">Indicates this request is for an automated search.</param>
    public void Deconstruct(out IVideo video, out bool isAutomatic)
    {
        video = Video;
        isAutomatic = IsAutomatic;
    }
}

