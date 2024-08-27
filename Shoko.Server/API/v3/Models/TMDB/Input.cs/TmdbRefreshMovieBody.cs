using System.ComponentModel;

#nullable enable
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
    /// Also download crew and cast. Will respect global option if not set.
    /// </summary>
    public bool? DownloadCrewAndCast { get; set; } = null;

    /// <summary>
    /// Also download movie collection. Will respect global option if not set.
    /// </summary>
    public bool? DownloadCollections { get; set; } = null;

    /// <summary>
    /// If true, the refresh will be ran immediately.
    /// </summary>
    public bool Immediate { get; set; } = false;
}
