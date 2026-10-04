namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   Which of a source's links to write into a cross-reference file, and how.
/// </summary>
/// <remarks>
///   The provider IDs are compared with what the file writes, so a link to
///   nothing is matched by an empty string. A filter left out matches
///   everything.
/// </remarks>
public sealed record MetadataCrossReferenceExportOptions
{
    /// <summary>
    ///   The sections to write, when they have anything to write.
    /// </summary>
    public MetadataCrossReferenceSections Sections { get; init; } = MetadataCrossReferenceSections.All;

    /// <summary>
    ///   Only the links of this AniDB anime.
    /// </summary>
    public int? AnidbAnimeID { get; init; }

    /// <summary>
    ///   Only the film and episode links of this AniDB episode.
    /// </summary>
    public int? AnidbEpisodeID { get; init; }

    /// <summary>
    ///   Only the series links and episode links of this series of the source,
    ///   as its own ID.
    /// </summary>
    public string? ProviderSeriesID { get; init; }

    /// <summary>
    ///   Only the episode links to this episode of the source, as its own ID.
    /// </summary>
    public string? ProviderEpisodeID { get; init; }

    /// <summary>
    ///   Only the film links to this film of the source, as its own ID.
    /// </summary>
    public string? ProviderMovieID { get; init; }

    /// <summary>
    ///   <c>true</c> for only the links made automatically,
    ///   <c>false</c> for only the ones a person verified, or
    ///   <c>null</c> for both.
    /// </summary>
    public bool? Automatic { get; init; }

    /// <summary>
    ///   <c>true</c> for only the series and episode links that
    ///   reach an episode of the source, <c>false</c> for only the
    ///   ones that do not, or <c>null</c> for both.
    /// </summary>
    public bool? WithEpisodes { get; init; }

    /// <summary>
    ///   Whether to add comments naming what each link joins, for the people
    ///   reading the file. They are skipped on import.
    /// </summary>
    public bool IncludeComments { get; init; }
}
