using System.Xml.Serialization;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Represents the type of a title.
/// </summary>
public enum TitleType
{
    /// <summary>
    /// No type specified.
    /// </summary>
    [XmlEnum("none")]
    None = 0,

    /// <summary>
    /// Main title.
    /// </summary>
    [XmlEnum("main")]
    Main = 1,

    /// <summary>
    /// Official title.
    /// </summary>
    [XmlEnum("official")]
    Official = 2,

    /// <summary>
    /// Short title.
    /// </summary>
    [XmlEnum("short")]
    Short = 3,

    /// <summary>
    /// Synonym title.
    /// </summary>
    [XmlEnum("syn")]
    Synonym = 4,

    /// <summary>
    /// Title card.
    /// </summary>
    [XmlEnum("card")]
    TitleCard = 5,

    /// <summary>
    /// Kana reading of the kanji title.
    /// </summary>
    [XmlEnum("kana")]
    KanjiReading = 6,
}

