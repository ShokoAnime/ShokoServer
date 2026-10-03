using Shoko.Abstractions.Metadata;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   The addresses of TMDb's own pages.
/// </summary>
public static class TmdbSiteUrls
{
    #region Constants

    /// <summary>
    ///   The site every TMDb page is under.
    /// </summary>
    public const string BaseUrl = "https://www.themoviedb.org";

    #endregion

    #region Pages

    /// <summary>
    ///   A show's page.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <returns>The URL.</returns>
    public static string Show(int showID)
        => $"{BaseUrl}/tv/{TmdbIds.Format(showID)}";

    /// <summary>
    ///   A season's page, under its show.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="seasonNumber">The season number, <c>0</c> for the specials.</param>
    /// <returns>The URL.</returns>
    public static string Season(int showID, int seasonNumber)
        => $"{Show(showID)}/season/{TmdbIds.Format(seasonNumber)}";

    /// <summary>
    ///   An episode's page, under its show and season.
    /// </summary>
    /// <param name="showID">The TMDb show ID.</param>
    /// <param name="seasonNumber">The season number, <c>0</c> for the specials.</param>
    /// <param name="episodeNumber">The episode number within the season.</param>
    /// <returns>The URL.</returns>
    public static string Episode(int showID, int seasonNumber, int episodeNumber)
        => $"{Season(showID, seasonNumber)}/episode/{TmdbIds.Format(episodeNumber)}";

    /// <summary>
    ///   A movie's page.
    /// </summary>
    /// <param name="movieID">The TMDb movie ID.</param>
    /// <returns>The URL.</returns>
    public static string Movie(int movieID)
        => $"{BaseUrl}/movie/{TmdbIds.Format(movieID)}";

    /// <summary>
    ///   A collection's page.
    /// </summary>
    /// <param name="collectionID">The TMDb collection ID.</param>
    /// <returns>The URL.</returns>
    public static string Collection(int collectionID)
        => $"{BaseUrl}/collection/{TmdbIds.Format(collectionID)}";

    /// <summary>
    ///   A person's page.
    /// </summary>
    /// <param name="personID">The TMDb person ID.</param>
    /// <returns>The URL.</returns>
    public static string Person(int personID)
        => $"{BaseUrl}/person/{TmdbIds.Format(personID)}";

    /// <summary>
    ///   A company's page.
    /// </summary>
    /// <param name="companyID">The TMDb company ID.</param>
    /// <returns>The URL.</returns>
    public static string Company(int companyID)
        => $"{BaseUrl}/company/{TmdbIds.Format(companyID)}";

    /// <summary>
    ///   A network's page.
    /// </summary>
    /// <param name="networkID">The TMDb network ID.</param>
    /// <returns>The URL.</returns>
    public static string Network(int networkID)
        => $"{BaseUrl}/network/{TmdbIds.Format(networkID)}";

    #endregion

    #region Entries

    /// <summary>
    ///   The page of a TMDb entry. A season or episode is read off the stored
    ///   entry, which names its show and numbers; every other kind off its ID.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>
    ///   The URL, or <see langword="null"/> for an entry of another source, a
    ///   kind without a page, a group of an ordering, a season or episode
    ///   known only by its ID, or an ID that is not a positive number.
    /// </returns>
    public static string? ForEntry(IMetadata entry)
    {
        var id = entry.ID;
        if (id.Source != MetadataSource.TMDB || !id.TryGetNumericID<int>(out var number) || number <= 0)
            return null;

        switch (entry)
        {
            case ISeason season when id.EntityType == MetadataEntityType.Season:
                return TmdbIds.TryGetID(season.SeriesID, MetadataEntityType.Series, out var seasonShowID) ? Season(seasonShowID, season.SeasonNumber) : null;
            case IEpisode episode when id.EntityType == MetadataEntityType.Episode:
                return TmdbIds.TryGetID(episode.SeriesID, MetadataEntityType.Series, out var episodeShowID) && episode.SeasonNumber is { } seasonNumber
                    ? Episode(episodeShowID, seasonNumber, episode.EpisodeNumber)
                    : null;
        }

        var kind = id.EntityType;
        if (kind == MetadataEntityType.Series)
            return Show(number);
        if (kind == MetadataEntityType.Movie)
            return Movie(number);
        if (kind == MetadataEntityType.Collection)
            return Collection(number);
        if (kind == MetadataEntityType.Creator)
            return Person(number);
        if (kind == MetadataEntityType.Studio)
            return Company(number);
        if (kind == MetadataEntityType.Network)
            return Network(number);

        return null;
    }

    #endregion
}
