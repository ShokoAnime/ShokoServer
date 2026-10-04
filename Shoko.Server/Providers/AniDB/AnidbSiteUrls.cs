using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Providers.AniDB;

/// <summary>
///   The addresses of AniDB's own pages, as the WebUI links to them.
/// </summary>
public static class AnidbSiteUrls
{
    #region Constants

    /// <summary>
    ///   The site every AniDB page is under.
    /// </summary>
    public const string BaseUrl = "https://anidb.net";

    #endregion

    #region Pages

    /// <summary>
    ///   An anime's page.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>The URL.</returns>
    public static string Anime(int animeID)
        => $"{BaseUrl}/anime/{animeID}";

    /// <summary>
    ///   An episode's page.
    /// </summary>
    /// <param name="episodeID">The AniDB episode ID.</param>
    /// <returns>The URL.</returns>
    public static string Episode(int episodeID)
        => $"{BaseUrl}/episode/{episodeID}";

    /// <summary>
    ///   A creator's page, which is also the page of a creator standing as a
    ///   studio.
    /// </summary>
    /// <param name="creatorID">The AniDB creator ID.</param>
    /// <returns>The URL.</returns>
    public static string Creator(int creatorID)
        => $"{BaseUrl}/creator/{creatorID}";

    /// <summary>
    ///   A character's page.
    /// </summary>
    /// <param name="characterID">The AniDB character ID.</param>
    /// <returns>The URL.</returns>
    public static string Character(int characterID)
        => $"{BaseUrl}/character/{characterID}";

    #endregion

    #region Entries

    /// <summary>
    ///   The page of an AniDB entry, read off its ID alone.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>
    ///   The URL, or <c>null</c> for an entry of another source,
    ///   a kind without a page or an ID that is not a positive number.
    /// </returns>
    public static string? ForEntry(IMetadata entry)
    {
        var id = entry.ID;
        if (id.Source != MetadataSource.AniDB || !id.TryGetNumericID<int>(out var number) || number <= 0)
            return null;

        var kind = id.EntityType;
        if (kind == MetadataEntityType.Series)
            return Anime(number);
        if (kind == MetadataEntityType.Episode)
            return Episode(number);
        if (kind == MetadataEntityType.Creator || kind == MetadataEntityType.Studio)
            return Creator(number);
        if (kind == MetadataEntityType.Character)
            return Character(number);

        return null;
    }

    #endregion
}
