
namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Type of series.
/// </summary>
public enum AnimeType
{
    /// <summary>
    /// A movie. A self-contained story.
    /// </summary>
    Movie = 0,

    /// <summary>
    /// An original Video Animation (OVA). A short series of episodes, not broadcast on TV.
    /// </summary>
    OVA = 1,

    /// <summary>
    /// A TV series. A series of episodes that are broadcast on TV.
    /// </summary>
    TVSeries = 2,

    /// <summary>
    /// A TV special. A special episode of a TV series.
    /// </summary>
    TVSpecial = 3,

    /// <summary>
    /// A web series. A series of episodes that are released on the web.
    /// </summary>
    Web = 4,

    /// <summary>
    /// Other misc. types of series not listed in this enum.
    /// </summary>
    Other = 5,
}
