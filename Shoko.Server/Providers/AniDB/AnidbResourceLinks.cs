using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.Providers.AniDB;

/// <summary>
///   Turns stored AniDB resources into the links the abstractions and the
///   API show, with a name and a URL for every type Shoko knows.
/// </summary>
public static class AnidbResourceLinks
{
    #region Links

    /// <summary>
    ///   The links of a list of stored resources, in their order.
    /// </summary>
    /// <param name="rows">The stored resources.</param>
    /// <returns>One link per resource.</returns>
    public static List<Resource> ToResources(IEnumerable<AniDB_Resource> rows)
        => [.. rows.Select(ToResource)];

    /// <summary>
    ///   The link of one stored resource. A type Shoko does not know keeps
    ///   its raw URL, or its raw identifiers with an empty URL.
    /// </summary>
    /// <param name="row">The stored resource.</param>
    /// <returns>The link.</returns>
    public static Resource ToResource(AniDB_Resource row)
    {
        var ids = row.Identifiers;
        var first = ids.Count > 0 ? ids[0] : null;
        var second = ids.Count > 1 ? ids[1] : null;
        var url = row.Urls.Count > 0 ? row.Urls[0] : null;
        var isEpisode = row.EpisodeID is not null;
        return row.ResourceType switch
        {
            ResourceLinkType.ANN when first is not null
                => CrossReference("AnimeNewsNetwork", $"https://www.animenewsnetwork.com/encyclopedia/anime.php?id={first}", first),
            ResourceLinkType.MAL when first is not null
                => CrossReference("MyAnimeList", $"https://myanimelist.net/anime/{first}", first),
            ResourceLinkType.AnimeNfo when first is not null && second is not null
                => CrossReference("AnimeNfo", $"https://www.animenfo.com/animetitle,{first},{second},a.html", first),
            ResourceLinkType.Site_JP when url is not null
                => new() { Type = ResourceType.Website, Name = "Official Site (JP)", Url = url, LanguageCode = "ja" },
            ResourceLinkType.Site_EN when url is not null
                => new() { Type = ResourceType.Website, Name = "Official Site (EN)", Url = url, LanguageCode = "en" },
            ResourceLinkType.Wiki_EN when first is not null
                => Wikipedia("EN", "en", first),
            ResourceLinkType.Wiki_JP when first is not null
                => Wikipedia("JP", "ja", first),
            ResourceLinkType.Wiki_KO when first is not null
                => Wikipedia("KO", "ko", first),
            ResourceLinkType.Wiki_ZH when first is not null
                => Wikipedia("ZH", "zh", first),
            ResourceLinkType.Syoboi when first is not null
                => CrossReference("syoboi", $"https://cal.syoboi.jp/tid/{first}/time", first),
            ResourceLinkType.ALLCinema when first is not null
                => CrossReference("allcinema", $"https://allcinema.net/cinema/{first}", first),
            ResourceLinkType.Anison when first is not null
                => CrossReference("Anison", $"https://anison.info/data/program/{first}.html", first),
            ResourceLinkType.DotLain when first is not null
                => CrossReference(".lain", $"https://lain.gr.jp/{first}", first.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? first),
            ResourceLinkType.VNDB when first is not null
                => CrossReference("VNDB", $"https://vndb.org/{second ?? "v"}{first}", $"{second ?? "v"}{first}"),
            ResourceLinkType.Bangumi when first is not null
                => CrossReference("bangumi", $"https://bgm.tv/subject/{first}", first),
            ResourceLinkType.Douban when first is not null
                => CrossReference("Douban", $"https://movie.douban.com/subject/{first}/", first),
            ResourceLinkType.IMDb when first is not null
                => CrossReference("IMDb", $"https://www.imdb.com/title/{first}/", first),
            ResourceLinkType.TMDB when first is not null && second is "tv" or "movie"
                => CrossReference("TMDB", $"https://www.themoviedb.org/{second}/{first}", first),
            ResourceLinkType.BaiduBaike when first is not null
                => new() { Type = ResourceType.Metadata, Name = "Baidu Baike", Url = $"https://baike.baidu.com/item/{first}", LanguageCode = "zh" },
            ResourceLinkType.Facebook when first is not null
                => new() { Type = ResourceType.Social, Name = "Facebook", Url = $"https://www.facebook.com/{first}", ID = first },
            ResourceLinkType.Twitter when first is not null
                => new() { Type = ResourceType.Social, Name = "X (Twitter)", Url = $"https://x.com/{first}", ID = first },
            ResourceLinkType.YouTube when first is not null
                => new() { Type = ResourceType.Trailer, Name = "YouTube", Url = $"https://www.youtube.com/{first}" },
            ResourceLinkType.Crunchyroll when first is not null && isEpisode
                => Streaming("Crunchyroll", $"https://www.crunchyroll.com/watch/{first}", first),
            ResourceLinkType.Crunchyroll when first is not null
                => Streaming("Crunchyroll", $"https://www.crunchyroll.com/series/{first}", first),
            ResourceLinkType.HiDive when first is not null
                => Streaming("HiDive", $"https://www.hidive.com/{first}", first),
            ResourceLinkType.Amazon when first is not null
                => Streaming("Amazon", $"https://www.amazon.com/dp/{first}", first),
            ResourceLinkType.Netflix when first is not null
                => Streaming("Netflix", $"https://www.netflix.com/title/{first}", first),
            ResourceLinkType.TencentVideo when first is not null
                => Streaming("Tencent Video", $"https://v.qq.com/detail/{first}", first),
            ResourceLinkType.Bilibili when first is not null
                => Streaming("Bilibili", $"https://www.bilibili.com/{first}", first),
            ResourceLinkType.PrimeVideo when first is not null
                => Streaming("Prime Video", $"https://www.primevideo.com/detail/{first}", first),
            ResourceLinkType.Funimation when first is not null
                => Streaming("Funimation", $"https://www.funimation.com/shows/{first.TrimEnd('/')}/", first.TrimEnd('/')),
            ResourceLinkType.OfficialStream when url is not null
                => new() { Type = ResourceType.Streaming, Name = "Official Stream", Url = url },
            ResourceLinkType.Marumegane when first is not null
                => new() { Type = ResourceType.CrossReference, Name = "Marumegane", Url = string.Empty, ID = first },
            _ => Raw(row),
        };
    }

    #endregion

    #region Cross-Source IDs

    /// <summary>
    ///   The IDs other sources gave an anime, as its AniDB resources name
    ///   them: TMDB's show or movie, and IMDb's title.
    /// </summary>
    /// <remarks>
    ///   An IMDb title counts as a movie for a movie anime, else as a series.
    /// </remarks>
    /// <param name="rows">The anime's stored resources.</param>
    /// <param name="isMovie">Whether the anime is a movie.</param>
    /// <returns>The IDs, in the resources' order, without repeats.</returns>
    public static List<MetadataGuid> ToCrossSourceIDs(IEnumerable<AniDB_Resource> rows, bool isMovie)
        => [.. rows.Select(row => ToCrossSourceID(row, isMovie)).OfType<MetadataGuid>().Distinct()];

    /// <summary>
    ///   The ID another source gave an anime, as one AniDB resource names it.
    /// </summary>
    /// <param name="row">The stored resource.</param>
    /// <param name="isMovie">Whether the anime is a movie.</param>
    /// <returns>The ID, or <see langword="null"/> when the resource names none.</returns>
    private static MetadataGuid? ToCrossSourceID(AniDB_Resource row, bool isMovie)
    {
        var ids = row.Identifiers;
        var first = ids.Count > 0 ? ids[0] : null;
        var second = ids.Count > 1 ? ids[1] : null;
        return row.ResourceType switch
        {
            ResourceLinkType.TMDB when second is "tv" => CrossSourceID.For("tmdb", MetadataEntityType.Series, first),
            ResourceLinkType.TMDB when second is "movie" => CrossSourceID.For("tmdb", MetadataEntityType.Movie, first),
            ResourceLinkType.IMDb => CrossSourceID.For("imdb", isMovie ? MetadataEntityType.Movie : MetadataEntityType.Series, first),
            _ => null,
        };
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   A link to the same entry in another database.
    /// </summary>
    /// <param name="name">The site's name.</param>
    /// <param name="url">The entry's page.</param>
    /// <param name="id">The entry's ID on the site.</param>
    /// <returns>The link.</returns>
    private static Resource CrossReference(string name, string url, string id)
        => new() { Type = ResourceType.CrossReference, Name = name, Url = url, ID = id };

    /// <summary>
    ///   A link to a streaming service's page.
    /// </summary>
    /// <param name="name">The service's name.</param>
    /// <param name="url">The page.</param>
    /// <param name="id">The ID the service knows the page by.</param>
    /// <returns>The link.</returns>
    private static Resource Streaming(string name, string url, string id)
        => new() { Type = ResourceType.Streaming, Name = name, Url = url, ID = id };

    /// <summary>
    ///   A link to a Wikipedia page.
    /// </summary>
    /// <param name="label">The label shown after the name, e.g. <c>EN</c>.</param>
    /// <param name="languageCode">The Wikipedia's language code, e.g. <c>en</c>.</param>
    /// <param name="title">
    ///   The page title, as AniDB gives it: maybe already percent-encoded,
    ///   and maybe with a <c>#</c> and a section anchor after it.
    /// </param>
    /// <returns>The link.</returns>
    private static Resource Wikipedia(string label, string languageCode, string title)
    {
        var anchorIndex = title.IndexOf('#');
        var page = anchorIndex < 0 ? title : title[..anchorIndex];
        var anchor = anchorIndex < 0 ? string.Empty : $"#{WikipediaEscape(title[(anchorIndex + 1)..])}";
        return new()
        {
            Type = ResourceType.Metadata,
            Name = $"Wikipedia ({label})",
            Url = $"https://{languageCode}.wikipedia.org/wiki/{WikipediaEscape(page)}{anchor}",
            LanguageCode = languageCode,
        };
    }

    /// <summary>
    ///   Percent-encodes a part of a Wikipedia link once, decoding it first
    ///   in case AniDB already encoded it, and keeping <c>/</c> and
    ///   <c>:</c> as MediaWiki does.
    /// </summary>
    /// <param name="value">The page title or section anchor.</param>
    /// <returns>The encoded value.</returns>
    private static string WikipediaEscape(string value)
        => Uri.EscapeDataString(Uri.UnescapeDataString(value))
            .Replace("%2F", "/")
            .Replace("%3A", ":");

    /// <summary>
    ///   The link of a resource of a type Shoko does not know, with its raw
    ///   URL or identifiers.
    /// </summary>
    /// <param name="row">The stored resource.</param>
    /// <returns>The link.</returns>
    private static Resource Raw(AniDB_Resource row)
        => new()
        {
            Type = ResourceType.Other,
            Name = $"AniDB Resource ({(int)row.ResourceType})",
            Url = row.Urls.Count > 0 ? row.Urls[0] : string.Empty,
            ID = row.Identifiers.Count > 0 ? string.Join('/', row.Identifiers) : null,
        };

    #endregion
}
