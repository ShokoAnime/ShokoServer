using System;
using System.Net;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anilist.Enums;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Helpers shared by the AniList integration.
/// </summary>
public static partial class AnilistUtility
{
    [GeneratedRegex(@"<br\s*/?>|</p>|</div>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LineBreakTagRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[ \t]*\r?\n[ \t]*(?:\r?\n[ \t]*)+", RegexOptions.Compiled)]
    private static partial Regex BlankLinesRegex();

    [GeneratedRegex(@"^(?<role>.*?)\s*\((?<qualifier>[^()]+)\)\s*$", RegexOptions.Compiled)]
    private static partial Regex RoleQualifierRegex();

    /// <summary>
    /// Split the language qualifier AniList appends to dub staff roles, such
    /// as "ADR Director (English)", off the role text. Other qualifiers,
    /// like episode ranges or "(OP)", are left in place.
    /// </summary>
    /// <param name="role">The role text as AniList reports it.</param>
    /// <returns>The role without the language, and the language if one was appended.</returns>
    public static (string Role, TitleLanguage? Language) SplitRoleLanguage(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return (string.Empty, null);

        role = role.Trim();
        if (RoleQualifierRegex().Match(role) is { Success: true } match && match.Groups["qualifier"].Value.Trim().TryGetTitleLanguage(out var language))
            return (match.Groups["role"].Value.Trim(), language);

        return (role, null);
    }

    /// <summary>
    /// Get the transcription language used for the main title, based on the
    /// anime's original language. AniList calls the field "romaji", but for
    /// chinese anime it holds pinyin, and for korean anime a korean
    /// transcription.
    /// </summary>
    /// <param name="originalLanguageCode">ISO 639-1 language code of the original language.</param>
    /// <returns>The transcription language.</returns>
    public static TitleLanguage GetMainTitleLanguage(string? originalLanguageCode)
        => originalLanguageCode?.Split('-')[0].ToLowerInvariant() switch
        {
            "zh" => TitleLanguage.Pinyin,
            "ko" => TitleLanguage.KoreanTranscription,
            "th" => TitleLanguage.ThaiTranscription,
            _ => TitleLanguage.Romaji,
        };

    /// <summary>
    /// AniList descriptions are HTML even when requested as plain text. Turn
    /// the line-break tags into newlines, drop every other tag, decode the
    /// entities and collapse runs of blank lines.
    /// </summary>
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = LineBreakTagRegex().Replace(html, "\n");
        text = TagRegex().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = BlankLinesRegex().Replace(text.Replace("\r\n", "\n"), "\n\n");
        return text.Trim();
    }

    /// <summary>
    /// Number of bits reserved for the episode number in a synthesized
    /// episode ID. Leaves 19 bits for the anime ID, which fills the positive
    /// <see cref="int"/> range exactly.
    /// </summary>
    private const int EpisodeBits = 12;

    /// <summary>
    /// The highest AniList anime ID that can be packed into a synthesized
    /// episode ID without overflowing into the sign bit.
    /// </summary>
    public const int MaxAnimeID = (1 << (31 - EpisodeBits)) - 1; // 524,287

    /// <summary>
    /// The highest episode number that can be packed into a synthesized
    /// episode ID.
    /// </summary>
    public const int MaxEpisodeNumber = (1 << EpisodeBits) - 1; // 4,095

    /// <summary>
    /// Check whether the anime ID and episode number fit in a synthesized
    /// episode ID.
    /// </summary>
    /// <param name="anilistAnimeId">The AniList anime ID.</param>
    /// <param name="episodeNumber">The episode number.</param>
    /// <returns><see langword="true"/> if the pair can be packed.</returns>
    public static bool CanPackEpisodeID(int anilistAnimeId, int episodeNumber)
        => anilistAnimeId is > 0 and <= MaxAnimeID && episodeNumber is >= 0 and <= MaxEpisodeNumber;

    /// <summary>
    /// Synthesize a stable episode ID from an AniList anime ID and an episode
    /// number. AniList has no episode entity, so the ID is a bijection of the
    /// pair and can be decoded again with <see cref="UnpackEpisodeID"/>.
    /// </summary>
    /// <param name="anilistAnimeId">The AniList anime ID. Must be at most <see cref="MaxAnimeID"/>.</param>
    /// <param name="episodeNumber">The episode number. Must be at most <see cref="MaxEpisodeNumber"/>.</param>
    /// <returns>The synthesized episode ID.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Either value is outside the packable range.</exception>
    public static int PackEpisodeID(int anilistAnimeId, int episodeNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(anilistAnimeId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(anilistAnimeId, MaxAnimeID);
        ArgumentOutOfRangeException.ThrowIfNegative(episodeNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(episodeNumber, MaxEpisodeNumber);
        return (anilistAnimeId << EpisodeBits) | episodeNumber;
    }

    /// <summary>
    /// Decode a synthesized episode ID back into the AniList anime ID and
    /// episode number it was made from.
    /// </summary>
    /// <param name="anilistEpisodeId">The synthesized episode ID.</param>
    /// <returns>The anime ID and episode number.</returns>
    public static (int AnilistAnimeID, int EpisodeNumber) UnpackEpisodeID(int anilistEpisodeId)
        => (anilistEpisodeId >> EpisodeBits, anilistEpisodeId & MaxEpisodeNumber);

    /// <summary>
    /// Map an AniList <c>MediaFormat</c> to an <see cref="AnimeType"/>.
    /// </summary>
    public static AnimeType ParseFormat(string? format) => format?.ToUpperInvariant() switch
    {
        "TV" => AnimeType.TVSeries,
        "TV_SHORT" => AnimeType.TVShort,
        "MOVIE" => AnimeType.Movie,
        "OVA" => AnimeType.OVA,
        "ONA" => AnimeType.Web,
        "SPECIAL" => AnimeType.TVSpecial,
        "MUSIC" => AnimeType.MusicVideo,
        _ => AnimeType.Unknown,
    };

    /// <summary>
    /// Map an <see cref="AnimeType"/> back to the AniList <c>MediaFormat</c>
    /// values it covers, for search filters.
    /// </summary>
    public static string[] ToFormats(AnimeType type) => type switch
    {
        AnimeType.TVSeries => ["TV"],
        AnimeType.TVShort => ["TV_SHORT"],
        AnimeType.Movie => ["MOVIE"],
        AnimeType.OVA => ["OVA"],
        AnimeType.Web => ["ONA"],
        AnimeType.TVSpecial => ["SPECIAL"],
        AnimeType.MusicVideo => ["MUSIC"],
        _ => [],
    };

    /// <summary>
    /// Map an AniList <c>MediaStatus</c> to an <see cref="AnilistMediaStatus"/>.
    /// </summary>
    public static AnilistMediaStatus ParseMediaStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "FINISHED" => AnilistMediaStatus.Finished,
        "RELEASING" => AnilistMediaStatus.Releasing,
        "NOT_YET_RELEASED" => AnilistMediaStatus.NotYetReleased,
        "CANCELLED" => AnilistMediaStatus.Cancelled,
        "HIATUS" => AnilistMediaStatus.Hiatus,
        _ => AnilistMediaStatus.Unknown,
    };

    /// <summary>
    /// Map an AniList <c>MediaSource</c> to an <see cref="AnilistMediaSource"/>.
    /// </summary>
    public static AnilistMediaSource ParseMediaSource(string? source) => source?.ToUpperInvariant() switch
    {
        "ORIGINAL" => AnilistMediaSource.Original,
        "MANGA" => AnilistMediaSource.Manga,
        "LIGHT_NOVEL" => AnilistMediaSource.LightNovel,
        "VISUAL_NOVEL" => AnilistMediaSource.VisualNovel,
        "VIDEO_GAME" => AnilistMediaSource.VideoGame,
        "NOVEL" => AnilistMediaSource.Novel,
        "DOUJINSHI" => AnilistMediaSource.Doujinshi,
        "ANIME" => AnilistMediaSource.Anime,
        "WEB_NOVEL" => AnilistMediaSource.WebNovel,
        "LIVE_ACTION" => AnilistMediaSource.LiveAction,
        "GAME" => AnilistMediaSource.Game,
        "COMIC" => AnilistMediaSource.Comic,
        "MULTIMEDIA_PROJECT" => AnilistMediaSource.MultimediaProject,
        "PICTURE_BOOK" => AnilistMediaSource.PictureBook,
        "OTHER" => AnilistMediaSource.Other,
        null or "" => AnilistMediaSource.Unknown,
        _ => AnilistMediaSource.Other,
    };

    /// <summary>
    /// Map an AniList <c>MediaSeason</c> to a <see cref="YearlySeason"/>.
    /// </summary>
    public static YearlySeason? ParseSeason(string? season) => season?.ToUpperInvariant() switch
    {
        "WINTER" => YearlySeason.Winter,
        "SPRING" => YearlySeason.Spring,
        "SUMMER" => YearlySeason.Summer,
        "FALL" => YearlySeason.Fall,
        _ => null,
    };

    /// <summary>
    /// Map a <see cref="YearlySeason"/> to the AniList <c>MediaSeason</c> value.
    /// </summary>
    public static string ToSeason(YearlySeason season) => season switch
    {
        YearlySeason.Winter => "WINTER",
        YearlySeason.Spring => "SPRING",
        YearlySeason.Summer => "SUMMER",
        _ => "FALL",
    };

    /// <summary>
    /// Map an AniList <c>countryOfOrigin</c> (ISO 3166-1 alpha-2) to the
    /// language code used for the native title.
    /// </summary>
    public static string ParseOriginalLanguage(string? countryOfOrigin) => countryOfOrigin?.ToUpperInvariant() switch
    {
        null or "" or "JP" => "ja",
        "CN" => "zh",
        "TW" => "zh-TW",
        "KR" => "ko",
        var other => other.ToLowerInvariant(),
    };
}
