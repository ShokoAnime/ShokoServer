using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   A year with cached AniDB anime in it, and its seasons.
/// </summary>
/// <param name="Year">The year.</param>
/// <param name="Seasons">Its listed seasons, from winter to fall.</param>
public sealed record AnidbAnimeSeasonYear(int Year, IReadOnlyList<AnidbAnimeSeasonCount> Seasons);
