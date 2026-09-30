using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.API.Converters;

/// <summary>
///   How the API spells a <see cref="MetadataSource"/> or a
///   <see cref="MetadataEntityType"/> in what it sends: the name the old
///   <c>DataSource</c> and <c>DataEntityType</c> enums wrote, so clients keep
///   reading what they always read. A source or entity type the old enums
///   never had, such as a plugin's own, is sent as its value.
/// </summary>
/// <remarks>
///   Only for output. Input takes a value, an alias or an old spelling alike,
///   ignoring case, so an old name is only sent while it reads back as the
///   same source or entity type; otherwise the value is sent.
/// </remarks>
internal static class LegacyMetadataSpellings
{
    #region Tables

    /// <summary>
    ///   The old <c>DataSource</c> name of each source value it had.
    /// </summary>
    private static readonly FrozenDictionary<string, string> _sources = new Dictionary<string, string>
    {
        ["anidb"] = "AniDB",
        ["tmdb"] = "TMDB",
        ["tvdb"] = "TvDB",
        ["anilist"] = "AniList",
        ["animeshon"] = "Animeshon",
        ["kitsu"] = "Kitsu",
        ["mal"] = "MAL",
        ["fanart-tv"] = "FanartTV",
        ["imdb"] = "IMDB",
        ["omdb"] = "OMDB",
        ["trakt"] = "TraktTv",
        ["tpdb"] = "TPDB",
        ["mediux"] = "MediUX",
        ["simkl"] = "SimKL",
        ["plugin"] = "Plugin",
        ["generated"] = "LocallyGenerated",
        ["user"] = "User",
        ["shoko"] = "Shoko",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///   The name the old <c>DataEntityType</c> enum was written as for each
    ///   entity type value it had. Where the enum had several names for one
    ///   number, this is the one its string converter picked, e.g.
    ///   <c>Show</c> rather than <c>Series</c>. A channel was a network
    ///   there, so it has no old name of its own and goes out as its value.
    /// </summary>
    private static readonly FrozenDictionary<string, string> _entityTypes = new Dictionary<string, string>
    {
        ["collection"] = "Franchise",
        ["series"] = "Show",
        ["season"] = "Season",
        ["episode"] = "Episode",
        ["movie"] = "Movie",
        ["video"] = "Video",
        ["studio"] = "Studio",
        ["network"] = "Network",
        ["creator"] = "Creator",
        ["character"] = "Character",
        ["user"] = "User",
        ["library"] = "Library",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    #endregion

    #region Lookups

    /// <summary>
    ///   Gets how the API spells a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The old name, or the value for a source the old enum lacked.</returns>
    public static string Of(MetadataSource source)
        => OfSourceValue(source.Value);

    /// <summary>
    ///   Gets how the API spells an entity type.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>The old name, or the value for an entity type the old enum lacked.</returns>
    public static string Of(MetadataEntityType entityType)
        => OfEntityTypeValue(entityType.Value);

    /// <summary>
    ///   Gets how the API spells a source from its value, for text already
    ///   turned into a value, such as a dictionary key. An old name that would
    ///   not be read back as the same source, such as <c>FanartTV</c> while no
    ///   plugin has registered it as an alias, gives way to the value.
    /// </summary>
    /// <param name="value">The source's value.</param>
    /// <returns>The old name, or the value unchanged.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? OfSourceValue(string? value)
        => value is not null && _sources.TryGetValue(value, out var name) &&
            (string.Equals(name, value, StringComparison.OrdinalIgnoreCase) || (MetadataSource.TryGet(name, out var source) && source.Value == value))
            ? name
            : value;

    /// <summary>
    ///   Gets how the API spells an entity type from its value, for text
    ///   already turned into a value, such as a dictionary key. An old name
    ///   that would not be read back as the same entity type gives way to the
    ///   value.
    /// </summary>
    /// <param name="value">The entity type's value.</param>
    /// <returns>The old name, or the value unchanged.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? OfEntityTypeValue(string? value)
        => value is not null && _entityTypes.TryGetValue(value, out var name) &&
            (string.Equals(name, value, StringComparison.OrdinalIgnoreCase) || (MetadataEntityType.TryGet(name, out var entityType) && entityType.Value == value))
            ? name
            : value;

    #endregion
}
