using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Represents an entity with yearly seasons.
/// </summary>
/// <remarks>
///   <para>
///     An entity starts in the season of its first regular episode, or of
///     its start date without dated ones. For the start, a season is a run
///     of whole weeks, Monday to Sunday, starting with the week holding its
///     first day (1 January, April, July or October) less a lead-in that
///     depends on the type: four weeks for TV series and shorts, one for
///     everything else. A lone premiere date up to two weeks before a
///     season, set apart by two or more episodes or a gap of more than ten
///     days and followed by two or more dates in that season, starts it in
///     that season. Five or more episodes released together with nothing in
///     the four weeks after them take no lead-in.
///   </para>
///   <para>
///     After its start, an entity with dated regular episodes is in every
///     calendar quarter (January to March is Winter, and so on) holding one
///     of them up to the fourth from the end, so the last three never carry
///     it into a season, and a quarter it took a break through is left out.
///     Without them it goes on to the quarter three weeks before its end
///     date, or up to today while it is still airing.
///   </para>
/// </remarks>
public interface IWithYearlySeasons
{
    /// <summary>
    ///   The yearly seasons the entity aired in, oldest first, up to the
    ///   season under way.
    /// </summary>
    IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons { get; }
}
