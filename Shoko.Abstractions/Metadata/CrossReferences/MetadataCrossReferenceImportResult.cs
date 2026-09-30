using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   What an import of a cross-reference file did, or why it did nothing.
/// </summary>
public sealed record MetadataCrossReferenceImportResult
{
    /// <summary>
    ///   The lines that could not be read. When there are any, nothing was
    ///   written.
    /// </summary>
    public IReadOnlyList<MetadataCrossReferenceImportError> Errors { get; init; } = [];

    /// <summary>
    ///   Whether the file was read and written.
    /// </summary>
    public bool Succeeded => Errors.Count is 0;

    /// <summary>
    ///   How many links the file held, over every section.
    /// </summary>
    public int LinkCount { get; init; }

    /// <summary>
    ///   How many film links were added.
    /// </summary>
    public int MoviesAdded { get; init; }

    /// <summary>
    ///   How many film links changed how they were arrived at.
    /// </summary>
    public int MoviesUpdated { get; init; }

    /// <summary>
    ///   How many film links were already there as the file has them.
    /// </summary>
    public int MoviesKept { get; init; }

    /// <summary>
    ///   How many film links were removed, for an episode the file gave
    ///   others.
    /// </summary>
    public int MoviesRemoved { get; init; }

    /// <summary>
    ///   How many series links were added, from the series section or for an
    ///   episode link into a series the anime was not linked to.
    /// </summary>
    public int SeriesAdded { get; init; }

    /// <summary>
    ///   How many episode links were added.
    /// </summary>
    public int EpisodesAdded { get; init; }

    /// <summary>
    ///   How many episode links changed how they were arrived at.
    /// </summary>
    public int EpisodesUpdated { get; init; }

    /// <summary>
    ///   How many episode links were already there as the file has them.
    /// </summary>
    public int EpisodesKept { get; init; }

    /// <summary>
    ///   How many episode links were removed, for an episode the file gave
    ///   others.
    /// </summary>
    public int EpisodesRemoved { get; init; }

    /// <summary>
    ///   How many refreshes of films and series not stored yet were queued.
    /// </summary>
    public int RefreshesQueued { get; init; }
}

/// <summary>
///   A line of a cross-reference file that could not be read.
/// </summary>
/// <param name="Line">The line's number, from 1.</param>
/// <param name="Message">What is wrong with it.</param>
public sealed record MetadataCrossReferenceImportError(int Line, string Message);
