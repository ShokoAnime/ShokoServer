using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// What a tag describes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum TagKind : byte
{
    /// <summary>
    /// A descriptive tag: a theme, a setting, a trope.
    /// </summary>
    Tag = 0,

    /// <summary>
    /// A genre.
    /// </summary>
    Genre = 1,

    /// <summary>
    /// A keyword: a plain word or phrase a source files the entry under,
    /// such as TMDB's keywords, rather than a curated tag.
    /// </summary>
    Keyword = 2,
}
