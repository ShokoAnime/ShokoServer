using System.ComponentModel;

namespace Shoko.Server.API.v3.Models.TMDB.Input;

public class TmdbRefreshMovieBody
{
    /// <summary>
    /// Forcefully download an update even if we updated recently.
    /// </summary>
    public bool Force { get; set; } = false;

    /// <summary>
    /// Also download images.
    /// </summary>
    [DefaultValue(true)]
    public bool DownloadImages { get; set; } = true;

    /// <summary>
    /// Ignored. The crew and cast are always downloaded, and the people in
    /// them while TMDB's creator kind is enabled.
    /// </summary>
    public bool? DownloadCrewAndCast { get; set; } = null;

    /// <summary>
    /// Ignored. The movie's collection is fetched whenever TMDB's collection
    /// provider is enabled.
    /// </summary>
    public bool? DownloadCollections { get; set; } = null;

    /// <summary>
    /// If true, the refresh will be ran immediately.
    /// </summary>
    public bool Immediate { get; set; } = false;

    /// <summary>
    /// If true, the refresh will be skipped if the movie already exists.
    /// </summary>
    public bool SkipIfExists { get; set; } = false;
}
