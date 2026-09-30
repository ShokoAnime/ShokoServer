using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Search;

/// <summary>
///   One candidate a source offered for a search, before anything is fetched
///   or stored.
/// </summary>
/// <remarks>
///   Not an <see cref="ISeries"/> or an <see cref="IMovie"/>: a provider builds
///   these straight off its search response and only fetches what is chosen.
/// </remarks>
public abstract record MetadataSearchResult
{
    /// <summary>
    ///   The candidate's identity at the source. Its kind is
    ///   <see cref="MetadataEntityType.Series"/> for a
    ///   <see cref="MetadataSeriesSearchResult"/> and
    ///   <see cref="MetadataEntityType.Movie"/> for a
    ///   <see cref="MetadataMovieSearchResult"/>, so it can be linked as is.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The source that offered it.
    /// </summary>
    public MetadataSource Source => ID.Source;

    /// <summary>
    ///   The title the source leads with.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    ///   The title in the original language, where the source distinguishes
    ///   them.
    /// </summary>
    public string? OriginalTitle { get; init; }

    /// <summary>
    ///   Every other title the source knows it by: translations, synonyms and
    ///   aliases, unsorted and in any language.
    /// </summary>
    /// <remarks>
    ///   Fill them in when the source has them: matching reads them alongside
    ///   <see cref="Title"/> and <see cref="OriginalTitle"/>, and a title AniDB
    ///   knows is often only among them.
    /// </remarks>
    public IReadOnlyList<string> AlternateTitles { get; init; } = [];

    /// <summary>
    ///   The original language, as a code.
    /// </summary>
    public string? OriginalLanguageCode { get; init; }

    /// <summary>
    ///   A short description, where the source gives one.
    /// </summary>
    public string? Overview { get; init; }

    /// <summary>
    ///   Whether the source marks it as adult.
    /// </summary>
    /// <remarks>
    ///   False where a source does not say, which is not the same as it
    ///   saying no.
    /// </remarks>
    public bool IsRestricted { get; init; }

    /// <summary>
    ///   The community rating, normalised to a scale of one to ten.
    /// </summary>
    public decimal? UserRating { get; init; }

    /// <summary>
    ///   How many votes that rating rests on.
    /// </summary>
    public int? UserVotes { get; init; }

    /// <summary>
    ///   A poster or cover image, as a URL the server can fetch.
    /// </summary>
    /// <remarks>
    ///   A source that returns a path rather than a URL resolves it before
    ///   handing it over; the core has no way to know its image host.
    /// </remarks>
    public string? PosterUrl { get; init; }

    /// <summary>
    ///   A backdrop or banner image, as a URL the server can fetch.
    /// </summary>
    public string? BackdropUrl { get; init; }

    /// <summary>
    ///   Whatever the source calls its genres, unmapped.
    /// </summary>
    public IReadOnlyList<string> Genres { get; init; } = [];
}
