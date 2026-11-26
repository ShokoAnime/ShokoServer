using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   A yearly season and the days it runs, as the airing calendar files
///   entries in it.
/// </summary>
/// <param name="Year">The season's year.</param>
/// <param name="Season">The season.</param>
/// <param name="Start">Its first day.</param>
/// <param name="End">Its last day, the day before the next season starts.</param>
public sealed record SeasonBounds(int Year, YearlySeason Season, DateOnly Start, DateOnly End);
