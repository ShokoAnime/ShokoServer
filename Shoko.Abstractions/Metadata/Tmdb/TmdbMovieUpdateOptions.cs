namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
///   Options for updating a TMDB movie's metadata. Nullable properties
///   use settings defaults when not set.
/// </summary>
public sealed class TmdbMovieUpdateOptions
{
    /// <summary>
    ///   The TMDB movie ID to update.
    /// </summary>
    public required int MovieId { get; set; }

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
    ///   Whether to download collection info. If <c>null</c>, uses
    ///   settings default.
    /// </summary>
    public bool? DownloadCollections { get; set; }
}
