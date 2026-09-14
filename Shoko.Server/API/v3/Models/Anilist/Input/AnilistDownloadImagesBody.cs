#nullable enable
namespace Shoko.Server.API.v3.Models.Anilist.Input;

/// <summary>
/// Body for the image download action.
/// </summary>
public class AnilistDownloadImagesBody
{
    /// <summary>
    /// Re-download images that are already available.
    /// </summary>
    public bool Force { get; set; } = false;

    /// <summary>
    /// Run the download now instead of scheduling it.
    /// </summary>
    public bool Immediate { get; set; } = false;
}
