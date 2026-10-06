using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// Decides which section of a season layout takes an anime in a viewed
/// season, for the sections view and the season listings and counts alike.
/// </summary>
internal static class SeasonSectionMatcher
{
    /// <summary>
    /// The index of the first section of a layout that takes an anime in a
    /// viewed season.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="type">The anime's type.</param>
    /// <param name="startSeason">The season it starts in, or <c>null</c> when not known.</param>
    /// <param name="getEpisodeDuration">Gives its usual episode length, asked at most once and only when a section needs it.</param>
    /// <param name="season">The viewed season.</param>
    /// <returns>The index, or <c>-1</c> when no section takes it.</returns>
    public static int IndexOf(
        IReadOnlyList<SeasonSectionDefinition> layout,
        AnimeType type,
        (int Year, YearlySeason Season)? startSeason,
        Func<TimeSpan?> getEpisodeDuration,
        (int Year, YearlySeason Season) season
    )
    {
        var isContinuing = startSeason is { } start && start.CompareTo(season) < 0;
        bool? isHalfLength = null;
        for (var index = 0; index < layout.Count; index++)
        {
            var section = layout[index];
            if (section.Types is { } types && !types.Contains(type))
                continue;

            if (section.Continuing is { } continuing && continuing != isContinuing)
                continue;

            if (section.HalfLength is { } halfLength)
            {
                isHalfLength ??= getEpisodeDuration() is { } duration && duration < SeasonSectionDefinition.HalfLengthLimit;
                if (halfLength != isHalfLength)
                    continue;
            }

            return index;
        }

        return -1;
    }
}
