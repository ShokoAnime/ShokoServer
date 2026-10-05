using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   One section of a season view, with its anime.
/// </summary>
/// <param name="Definition">The section's definition.</param>
/// <param name="Anime">
///   The anime it took, sorted by next airing: the scheduled ones soonest
///   first, then those known only by an AniDB air date, then those with
///   neither by premiere, each tier then by title.
/// </param>
public sealed record SeasonSection(SeasonSectionDefinition Definition, IReadOnlyList<SeasonAnimeEntry> Anime);
