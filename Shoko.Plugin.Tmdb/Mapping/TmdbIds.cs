using System.Globalization;
using Shoko.Abstractions.Metadata;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   The identifiers of TMDB's entries under the <c>tmdb</c> source, and
///   the way back to TMDB's own IDs.
/// </summary>
/// <remarks>
///   A genre and a keyword share the tag kind, so each is filed under its
///   own prefix: <c>tmdb://tag/genre/16</c>, <c>tmdb://tag/keyword/210024</c>.
///   A genre joining two with <c>&amp;</c> is filed as one tag per part,
///   numbered from 1: <c>tmdb://tag/genre/10759/1</c>. An episode group
///   collection is an ordering and each of its groups a season, both by
///   TMDB's own string IDs.
/// </remarks>
public static class TmdbIds
{
    #region Constants

    /// <summary>
    ///   What a genre's tag ID starts with.
    /// </summary>
    public const string GenrePrefix = "genre/";

    /// <summary>
    ///   What a keyword's tag ID starts with.
    /// </summary>
    public const string KeywordPrefix = "keyword/";

    #endregion

    #region Building

    /// <summary>
    ///   A show.
    /// </summary>
    /// <param name="showID">The TMDB show ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Series(int showID)
        => Of(MetadataEntityType.Series, showID);

    /// <summary>
    ///   A show's season.
    /// </summary>
    /// <param name="seasonID">The TMDB season ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Season(int seasonID)
        => Of(MetadataEntityType.Season, seasonID);

    /// <summary>
    ///   A show's episode.
    /// </summary>
    /// <param name="episodeID">The TMDB episode ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Episode(int episodeID)
        => Of(MetadataEntityType.Episode, episodeID);

    /// <summary>
    ///   A movie.
    /// </summary>
    /// <param name="movieID">The TMDB movie ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Movie(int movieID)
        => Of(MetadataEntityType.Movie, movieID);

    /// <summary>
    ///   A collection of movies.
    /// </summary>
    /// <param name="collectionID">The TMDB collection ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Collection(int collectionID)
        => Of(MetadataEntityType.Collection, collectionID);

    /// <summary>
    ///   A person.
    /// </summary>
    /// <param name="personID">The TMDB person ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Creator(int personID)
        => Of(MetadataEntityType.Creator, personID);

    /// <summary>
    ///   A company, as a studio.
    /// </summary>
    /// <param name="companyID">The TMDB company ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Studio(int companyID)
        => Of(MetadataEntityType.Studio, companyID);

    /// <summary>
    ///   A network.
    /// </summary>
    /// <param name="networkID">The TMDB network ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Network(int networkID)
        => Of(MetadataEntityType.Network, networkID);

    /// <summary>
    ///   A genre, as a tag.
    /// </summary>
    /// <param name="genreID">The TMDB genre ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Genre(int genreID)
        => new(MetadataSource.TMDB, MetadataEntityType.Tag, GenrePrefix + Format(genreID));

    /// <summary>
    ///   One part of a genre that joins two, as a tag.
    /// </summary>
    /// <param name="genreID">The TMDB genre ID.</param>
    /// <param name="part">The part, from <c>1</c>.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid GenrePart(int genreID, int part)
        => new(MetadataSource.TMDB, MetadataEntityType.Tag, GenrePrefix + Format(genreID) + "/" + Format(part));

    /// <summary>
    ///   A keyword, as a tag.
    /// </summary>
    /// <param name="keywordID">The TMDB keyword ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Keyword(int keywordID)
        => new(MetadataSource.TMDB, MetadataEntityType.Tag, KeywordPrefix + Format(keywordID));

    /// <summary>
    ///   An episode group collection, as an ordering.
    /// </summary>
    /// <param name="collectionID">The episode group collection ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid Ordering(string collectionID)
        => new(MetadataSource.TMDB, MetadataEntityType.Ordering, collectionID);

    /// <summary>
    ///   An episode group, as a season of its ordering.
    /// </summary>
    /// <param name="groupID">The episode group ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid OrderingGroup(string groupID)
        => new(MetadataSource.TMDB, MetadataEntityType.Season, groupID);

    /// <summary>
    ///   An ID as TMDB writes it.
    /// </summary>
    /// <param name="id">The ID.</param>
    /// <returns>The ID as text.</returns>
    public static string Format(int id)
        => id.ToString(CultureInfo.InvariantCulture);

    private static MetadataGuid Of(MetadataEntityType entityType, int id)
        => new(MetadataSource.TMDB, entityType, Format(id));

    #endregion

    #region Reading

    /// <summary>
    ///   Reads TMDB's ID out of an entry of one kind.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <param name="entityType">The kind it must be.</param>
    /// <param name="tmdbID">TMDB's ID.</param>
    /// <returns><c>true</c> for a TMDB entry of the kind with a positive ID.</returns>
    public static bool TryGetID(MetadataGuid? id, MetadataEntityType entityType, out int tmdbID)
    {
        tmdbID = 0;
        return id is not null && id.Source == MetadataSource.TMDB && id.EntityType == entityType && id.TryGetNumericID(out tmdbID) && tmdbID > 0;
    }

    #endregion
}
