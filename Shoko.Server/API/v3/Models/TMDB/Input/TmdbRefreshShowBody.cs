using System.ComponentModel;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB.Input;

/// <summary>
/// Refresh or download the metadata for a TMDB show.
/// </summary>
public class TmdbRefreshShowBody
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
    /// Also download alternate ordering information. Will respect global option if not set.
    /// </summary>
    public bool? DownloadAlternateOrdering { get; set; } = null;

    /// <summary>
    /// Also download networks for show. Will respect global option if not set.
    /// </summary>
    public bool? DownloadNetworks { get; set; } = null;

    /// <summary>
    /// If true, the refresh will be ran immediately.
    /// </summary>
    public bool Immediate { get; set; } = false;

    /// <summary>
    /// If set to <see langword="true"/> and <see cref="Immediate"/> is also set
    /// to <see langword="true"/>, then the heavy operations will be postponed
    /// to run later in the background while the essential data necessary for a
    /// preview will be downloaded immediately.
    /// </summary>
    public bool QuickRefresh { get; set; } = false;
}
