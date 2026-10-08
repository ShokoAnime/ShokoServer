using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;

namespace Shoko.Plugin.Tmdb.Services;

/// <summary>
///   The core's stores and services the plugin writes through, together.
/// </summary>
/// <remarks>
///   The plugin keeps nothing of its own: every show, movie, collection,
///   person, studio, network, tag, suggestion, ordering and text it learns
///   from TMDB goes into these.
/// </remarks>
/// <param name="series">The store of shows with their seasons and episodes.</param>
/// <param name="movies">The store of movies.</param>
/// <param name="collections">The store of collections.</param>
/// <param name="people">The store of people and credits.</param>
/// <param name="studios">The store of studios and networks.</param>
/// <param name="tags">The store of tags, genres and keywords.</param>
/// <param name="suggestions">The store of suggestions.</param>
/// <param name="orderings">The ordering service.</param>
/// <param name="texts">The text manager.</param>
public sealed class TmdbStores(
    IMetadataSeriesStore series,
    IMetadataMovieStore movies,
    IMetadataCollectionStore collections,
    IMetadataPeopleStore people,
    IMetadataStudioStore studios,
    IMetadataTagStore tags,
    IMetadataSuggestionStore suggestions,
    IMetadataOrderingService orderings,
    IMetadataTextManager texts
)
{
    /// <summary>
    ///   The store of shows with their seasons and episodes.
    /// </summary>
    public IMetadataSeriesStore Series { get; } = series;

    /// <summary>
    ///   The store of movies.
    /// </summary>
    public IMetadataMovieStore Movies { get; } = movies;

    /// <summary>
    ///   The store of collections.
    /// </summary>
    public IMetadataCollectionStore Collections { get; } = collections;

    /// <summary>
    ///   The store of people and credits.
    /// </summary>
    public IMetadataPeopleStore People { get; } = people;

    /// <summary>
    ///   The store of studios and networks.
    /// </summary>
    public IMetadataStudioStore Studios { get; } = studios;

    /// <summary>
    ///   The store of tags, genres and keywords.
    /// </summary>
    public IMetadataTagStore Tags { get; } = tags;

    /// <summary>
    ///   The store of suggestions.
    /// </summary>
    public IMetadataSuggestionStore Suggestions { get; } = suggestions;

    /// <summary>
    ///   The ordering service, which keeps the episode groups as orderings.
    /// </summary>
    public IMetadataOrderingService Orderings { get; } = orderings;

    /// <summary>
    ///   The text manager, for the texts of entries the stores keep no room
    ///   for and the language order.
    /// </summary>
    public IMetadataTextManager Texts { get; } = texts;
}
