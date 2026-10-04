using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A movie to store, whole: its own fields and its titles and
///   overviews. Saving it replaces what was stored for the movie.
/// </summary>
public sealed record MetadataMovieData
{
    /// <summary>
    ///   The movie: its source, the <c>movie</c> kind and the source's own ID
    ///   for it, e.g. <c>anilist://movie/199</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The movie's titles, in order. They are stored under the movie's
    ///   source, whatever source each title names.
    /// </summary>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The movie's overviews, in order, stored like the titles.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   The day the movie was first released, when known.
    /// </summary>
    public DateOnly? ReleaseDate { get; init; }

    /// <summary>
    ///   How long the movie runs, or <c>null</c> when not known. Stored to
    ///   the second.
    /// </summary>
    public TimeSpan? Runtime { get; init; }

    /// <summary>
    ///   Whether the movie is for adults only.
    /// </summary>
    public bool Restricted { get; init; }

    /// <summary>
    ///   Whether it is a standalone video rather than a movie.
    /// </summary>
    public bool Video { get; init; }

    /// <summary>
    ///   The language the movie was first made in, as a language code, when
    ///   the source says. At most 32 characters.
    /// </summary>
    public string? OriginalLanguageCode { get; init; }

    /// <summary>
    ///   The countries the movie was made in, in order, as ISO 3166-1 codes
    ///   when the source gives them. A country given twice is kept once.
    /// </summary>
    public IReadOnlyList<string> ProductionCountries { get; init; } = [];

    /// <summary>
    ///   The source's user rating, on a scale of 1 to 10.
    /// </summary>
    public double Rating { get; init; }

    /// <summary>
    ///   How many votes the rating is made from.
    /// </summary>
    public int RatingVotes { get; init; }

    /// <summary>
    ///   Links to the movie elsewhere, such as its homepage.
    /// </summary>
    public IReadOnlyList<Resource> Resources { get; init; } = [];

    /// <summary>
    ///   The IDs other sources gave the same movie, as the source lists
    ///   them, e.g. <c>imdb://movie/tt0000001</c>. The source of each may be one
    ///   nobody registered. An ID given twice is kept once.
    /// </summary>
    public IReadOnlyList<MetadataGuid> CrossSourceIDs { get; init; } = [];

    /// <summary>
    ///   The movie's content ratings, in order, a country as often as it
    ///   is rated. A rating given twice for a country keeps its first place.
    /// </summary>
    public IReadOnlyList<MetadataContentRatingData> ContentRatings { get; init; } = [];

    /// <summary>
    ///   The collection the movie is part of, on the movie's own source, or
    ///   <c>null</c> when it is part of none. Kept whether or not the
    ///   collection itself is stored.
    /// </summary>
    public MetadataGuid? CollectionID { get; init; }

    /// <summary>
    ///   The source's resource ID of the movie's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the movie has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}
