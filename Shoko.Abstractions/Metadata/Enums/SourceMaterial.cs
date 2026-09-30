using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// What a work was adapted from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum SourceMaterial : byte
{
    /// <summary>
    /// The source does not say.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Not an adaptation.
    /// </summary>
    Original = 1,

    /// <summary>
    /// Something none of the other values name.
    /// </summary>
    Other = 2,

    /// <summary>
    /// A manga.
    /// </summary>
    Manga = 3,

    /// <summary>
    /// A light novel.
    /// </summary>
    LightNovel = 4,

    /// <summary>
    /// A visual novel.
    /// </summary>
    VisualNovel = 5,

    /// <summary>
    /// A video game.
    /// </summary>
    VideoGame = 6,

    /// <summary>
    /// A novel.
    /// </summary>
    Novel = 7,

    /// <summary>
    /// A self-published work.
    /// </summary>
    Doujinshi = 8,

    /// <summary>
    /// Another anime.
    /// </summary>
    Anime = 9,

    /// <summary>
    /// A novel published online.
    /// </summary>
    WebNovel = 10,

    /// <summary>
    /// A live-action film or series.
    /// </summary>
    LiveAction = 11,

    /// <summary>
    /// A game other than a video game.
    /// </summary>
    Game = 12,

    /// <summary>
    /// A comic other than a manga, a manhwa or a manhua.
    /// </summary>
    Comic = 13,

    /// <summary>
    /// A project spanning several media from the start.
    /// </summary>
    MultimediaProject = 14,

    /// <summary>
    /// A picture book.
    /// </summary>
    PictureBook = 15,

    /// <summary>
    /// An adult game, usually a visual novel.
    /// </summary>
    Eroge = 16,

    /// <summary>
    /// A Korean comic.
    /// </summary>
    Manhwa = 17,

    /// <summary>
    /// A Chinese comic.
    /// </summary>
    Manhua = 18,
}
