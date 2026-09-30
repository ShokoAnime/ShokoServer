namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   How to read a cross-reference file into a source's links.
/// </summary>
public sealed record MetadataCrossReferenceImportOptions
{
    /// <summary>
    ///   Whether an AniDB episode named in the file loses the film or episode
    ///   links the file does not give it.
    /// </summary>
    public bool RemoveExisting { get; init; } = true;

    /// <summary>
    ///   Whether to queue a refresh of each film the file links to an anime
    ///   in the library that is not stored yet.
    /// </summary>
    public bool AddMissingMovies { get; init; } = true;

    /// <summary>
    ///   Whether to queue a refresh of each series the file links to an anime
    ///   in the library, directly or through an episode, that is not stored
    ///   yet.
    /// </summary>
    public bool AddMissingSeries { get; init; } = true;
}
