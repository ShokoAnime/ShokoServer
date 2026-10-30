using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A season to store, as part of its series.
/// </summary>
public sealed record MetadataSeasonData
{
    /// <summary>
    ///   The season: its source, the <c>season</c> kind and the source's own
    ///   ID for it, e.g. <c>anilist://season/21-1</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The season's number in the series' own ordering.
    /// </summary>
    public required int SeasonNumber { get; init; }

    /// <summary>
    ///   The season's titles, in order, stored under the season's source.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the season's default.
    ///   Without one, the season gets its generic name, such as
    ///   <c>Season 2</c>, synthesized in the user's languages. Generic names are
    ///   left out, so a season named only by one is better saved without.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The season's overviews, in order, stored like the titles.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   The source's resource ID of the season's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the season has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}
