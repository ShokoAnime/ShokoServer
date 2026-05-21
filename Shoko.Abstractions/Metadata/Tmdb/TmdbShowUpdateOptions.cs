namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
///   Options for updating a TMDB show's metadata. Nullable properties
///   use settings defaults when not set.
/// </summary>
public sealed class TmdbShowUpdateOptions
{
    /// <summary>
    ///   The TMDB show ID to update.
    /// </summary>
    public required int ShowId { get; set; }

    /// <summary>
    ///   Force refresh even if recently updated.
    /// </summary>
    public bool ForceRefresh { get; set; }

    /// <summary>
    ///   Whether to download images.
    /// </summary>
    public bool DownloadImages { get; set; }

    /// <summary>
    ///   Whether to download crew and cast. If <c>null</c>, uses settings
    ///   default.
    /// </summary>
    public bool? DownloadCrewAndCast { get; set; }

    /// <summary>
    ///   Whether to download alternate ordering. If <c>null</c>, uses
    ///   settings default.
    /// </summary>
    public bool? DownloadAlternateOrdering { get; set; }

    /// <summary>
    ///   Whether to download network info. If <c>null</c>, uses settings
    ///   default.
    /// </summary>
    public bool? DownloadNetworks { get; set; }

    /// <summary>
    ///   Whether to perform a quick refresh (skip some operations).
    /// </summary>
    public bool QuickRefresh { get; set; }
}
