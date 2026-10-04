using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   A season with cached AniDB anime in it.
/// </summary>
/// <param name="Year">The year.</param>
/// <param name="Season">The season.</param>
/// <param name="Count">How many cached anime are in the season.</param>
/// <param name="IsCurrent">Whether the season is the one under way today.</param>
/// <param name="Poster">
///   The poster of the best ranked anime starting in the season that has
///   one, when asked for; <c>null</c> otherwise or when none has one.
/// </param>
/// <param name="Backdrop">
///   The backdrop of the anime the <paramref name="Poster"/> comes from;
///   <c>null</c> when not asked for or when it has none.
/// </param>
public sealed record AnidbAnimeSeasonCount(
    int Year,
    YearlySeason Season,
    int Count,
    bool IsCurrent,
    IImage? Poster = null,
    IImage? Backdrop = null
);
