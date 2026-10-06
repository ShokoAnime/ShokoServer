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
///     After its start, an entity is only in the calendar quarters (January
///     to March is Winter, and so on) holding one of its dated regular
///     episodes, so a quarter it took a break through is left out. Once it
///     has an end date (a season or ordering group: once its series has
///     one), the run has ended and only the episodes up to the fourth from
///     the end count, so its last three never carry it into a season. An
///     entity without dated regular episodes is in its start season alone.
///     For an AniDB anime, the
///     stored normal airings of the regular episodes AniDB gives no date, or
///     does not list yet, count as dated episodes; estimates never do.
///   </para>
///   <para>
///     A user may set the season an AniDB anime starts in by hand, which
///     carries over to its Shoko series and groups. It then starts in that
///     season and is in each season the rule places it in after it; a
///     later start drops the seasons before it, and an earlier one keeps
///     them all, with none filled in between.
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
