
#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB.Input;

public class TmdbDownloadImagesBody
{
    /// <summary>
    /// Forcefully re-download existing images, even if they're already cached.
    /// </summary>
    public bool Force { get; set; } = false;

    /// <summary>
    /// If true, the download will be ran immediately.
    /// </summary>
    public bool Immediate { get; set; } = false;
}
