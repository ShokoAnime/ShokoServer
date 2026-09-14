using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Shoko.Abstractions.Metadata.Anilist.Enums;

/// <summary>
/// Source type the media was adapted from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum AnilistMediaSource : byte
{
    /// <summary>
    /// Unknown or not yet reported.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///   An original production not based of another work.
    /// </summary>
    [DataMember(Name = "ORIGINAL")]
    [JsonStringEnumMemberName("ORIGINAL")]
    Original = 1,

    /// <summary>
    ///   Other.
    /// </summary>
    [DataMember(Name = "OTHER")]
    [JsonStringEnumMemberName("OTHER")]
    Other = 2,

    /// <summary>
    ///   Asian comic book.
    /// </summary>
    [DataMember(Name = "MANGA")]
    [JsonStringEnumMemberName("MANGA")]
    Manga = 3,

    /// <summary>
    ///   Written work published in volumes.
    /// </summary>
    [DataMember(Name = "LIGHT_NOVEL")]
    [JsonStringEnumMemberName("LIGHT_NOVEL")]
    LightNovel = 4,

    /// <summary>
    ///   Video game driven primary by text and narrative.
    /// </summary>
    [DataMember(Name = "VISUAL_NOVEL")]
    [JsonStringEnumMemberName("VISUAL_NOVEL")]
    VisualNovel = 5,

    /// <summary>
    ///   Video game.
    /// </summary>
    [DataMember(Name = "VIDEO_GAME")]
    [JsonStringEnumMemberName("VIDEO_GAME")]
    VideoGame = 6,

    /// <summary>
    ///   Version 2+ only. Written works not published in volumes.
    /// </summary>
    [DataMember(Name = "NOVEL")]
    [JsonStringEnumMemberName("NOVEL")]
    Novel = 7,

    /// <summary>
    ///   Version 2+ only. Self-published works.
    /// </summary>
    [DataMember(Name = "DOUJINSHI")]
    [JsonStringEnumMemberName("DOUJINSHI")]
    Doujinshi = 8,

    /// <summary>
    ///   Version 2+ only. Japanese Anime.
    /// </summary>
    [DataMember(Name = "ANIME")]
    [JsonStringEnumMemberName("ANIME")]
    Anime = 9,

    /// <summary>
    ///   Version 3 only. Written works published online.
    /// </summary>
    [DataMember(Name = "WEB_NOVEL")]
    [JsonStringEnumMemberName("WEB_NOVEL")]
    WebNovel = 10,

    /// <summary>
    ///   Version 3 only. Live action media such as movies or TV show.
    /// </summary>
    [DataMember(Name = "LIVE_ACTION")]
    [JsonStringEnumMemberName("LIVE_ACTION")]
    LiveAction = 11,

    /// <summary>
    ///   Version 3 only. Games excluding video games.
    /// </summary>
    [DataMember(Name = "GAME")]
    [JsonStringEnumMemberName("GAME")]
    Game = 12,

    /// <summary>
    ///   Version 3 only. Comics excluding manga.
    /// </summary>
    [DataMember(Name = "COMIC")]
    [JsonStringEnumMemberName("COMIC")]
    Comic = 13,

    /// <summary>
    ///   Version 3 only. Multimedia project.
    /// </summary>
    [DataMember(Name = "MULTIMEDIA_PROJECT")]
    [JsonStringEnumMemberName("MULTIMEDIA_PROJECT")]
    MultimediaProject = 14,

    /// <summary>
    ///   Version 3 only. Picture book.
    /// </summary>
    [DataMember(Name = "PICTURE_BOOK")]
    [JsonStringEnumMemberName("PICTURE_BOOK")]
    PictureBook = 15,
}
