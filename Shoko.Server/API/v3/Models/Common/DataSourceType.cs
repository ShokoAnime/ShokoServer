using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Available data sources to chose from.
/// </summary>
/// <remarks>
/// Covers only the sources the core serves itself, and is what a studio,
/// network or content rating names as its source. <c>includeDataFrom</c>
/// takes any <see cref="MetadataSource"/> instead, still reading these names
/// and numbers as it always did.
/// </remarks>
[JsonConverter(typeof(StringEnumConverter))]
public enum DataSourceType
{
    /// <summary>
    /// AniDB.
    /// </summary>
    AniDB = 0,

    /// <summary>
    /// The Movie DataBase (TMDB).
    /// </summary>
    TMDB = 1,

    /// <summary>
    /// AniList, which a plugin serves now. Kept so <c>includeDataFrom</c>
    /// still reads the name and the number as AniList's source, when the
    /// plugin has registered it.
    /// </summary>
    [Obsolete("AniList is served by a plugin; name its metadata source instead.")]
    AniList = 3,
}
