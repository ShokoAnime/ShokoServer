using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// An external link AniList lists for an anime: the official site, a
/// streaming service page, or a social media account.
/// </summary>
public class Anilist_Anime_ExternalLink : Anilist_Base<int>
{
    #region Database Columns

    public int Anilist_Anime_ExternalLinkID { get; set; }

    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// AniList's own ID for the link.
    /// </summary>
    public int AnilistLinkID { get; set; }

    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the site, e.g. "Crunchyroll" or "Official Site".
    /// </summary>
    public string Site { get; set; } = string.Empty;

    /// <summary>
    /// AniList's ID for the site, shared by every link to the same site.
    /// </summary>
    public int? AnilistSiteID { get; set; }

    /// <summary>
    /// AniList's link type: <c>INFO</c>, <c>STREAMING</c> or <c>SOCIAL</c>.
    /// </summary>
    public string LinkType { get; set; } = string.Empty;

    /// <summary>
    /// Language code of the linked page, when AniList knows it. Streaming
    /// links are usually per-region.
    /// </summary>
    public string? LanguageCode { get; set; }

    #endregion

    #region Constructors

    public Anilist_Anime_ExternalLink() { }

    public Anilist_Anime_ExternalLink(int anilistAnimeId, int anilistLinkId)
    {
        AnilistAnimeID = anilistAnimeId;
        AnilistLinkID = anilistLinkId;
    }

    #endregion

    #region Helpers

    /// <inheritdoc/>
    public override int Id => AnilistLinkID;

    public ResourceType ResourceType => LinkType.ToUpperInvariant() switch
    {
        "STREAMING" => ResourceType.Streaming,
        "SOCIAL" => ResourceType.Social,
        _ => ResourceType.Website,
    };

    public Resource ToResource()
        => new() { Type = ResourceType, Name = Site, Url = Url, LanguageCode = LanguageCode };

    public Anilist_Anime? Anime
        => RepoFactory.Anilist_Anime.GetByAnilistAnimeID(AnilistAnimeID);

    #endregion
}
