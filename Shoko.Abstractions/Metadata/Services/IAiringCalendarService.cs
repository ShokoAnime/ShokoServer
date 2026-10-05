using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Builds the airing calendar's views, sorted and grouped: a season's
///   anime by section, the airings of a range by local day and episode, and
///   the seasons by year.
/// </summary>
public interface IAiringCalendarService
{
    #region Seasons

    /// <summary>
    ///   Lists the cached AniDB anime of a yearly season, each with its next
    ///   airing and the other upcoming airings of that episode.
    /// </summary>
    /// <remarks>
    ///   The anime are read through
    ///   <c>IAnidbService.GetCachedAnime</c>, in the order the options ask
    ///   for. The airings are read through each anime's Shoko series, or the
    ///   anime itself outside the collection, and count from now.
    /// </remarks>
    /// <param name="year">The year.</param>
    /// <param name="season">The season.</param>
    /// <param name="animeOptions">The anime filters and order; its seasons are replaced by this one.</param>
    /// <param name="airingOptions">The airing filters; the read is always next-only.</param>
    /// <param name="today">The current date, which decides whether an anime has finished. Defaults to today in UTC.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="year"/> is not between 1 and 9999.</exception>
    /// <returns>The anime.</returns>
    IReadOnlyList<SeasonAnimeEntry> GetSeasonAnime(
        int year,
        YearlySeason season,
        AnidbAnimeListOptions? animeOptions = null,
        EpisodeAiringFilteringOptions? airingOptions = null,
        DateOnly? today = null
    );

    /// <summary>
    ///   Lists the cached AniDB anime of a yearly season by section, as
    ///   <see cref="GetSeasonAnime"/> reads them.
    /// </summary>
    /// <remarks>
    ///   Each anime goes to the first section that takes it, an anime no
    ///   section takes is left out, and empty sections are dropped.
    /// </remarks>
    /// <param name="year">The year.</param>
    /// <param name="season">The season.</param>
    /// <param name="sections">The layout, or <c>null</c> for <see cref="SeasonSectionDefinition.DefaultLayout"/>.</param>
    /// <param name="animeOptions">The anime filters; its seasons are replaced by this one.</param>
    /// <param name="airingOptions">The airing filters; the read is always next-only.</param>
    /// <param name="today">The current date, which decides whether an anime has finished. Defaults to today in UTC.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="year"/> is not between 1 and 9999.</exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="sections"/> holds a <c>null</c> section, or two
    ///   sections with the same ID.
    /// </exception>
    /// <returns>The non-empty sections, in the layout's order.</returns>
    IReadOnlyList<SeasonSection> GetSeasonSections(
        int year,
        YearlySeason season,
        IReadOnlyList<SeasonSectionDefinition>? sections = null,
        AnidbAnimeListOptions? animeOptions = null,
        EpisodeAiringFilteringOptions? airingOptions = null,
        DateOnly? today = null
    );

    /// <summary>
    ///   Lists the seasons the cached AniDB anime are in, as
    ///   <c>IAnidbService.GetCachedAnimeSeasons</c> does, grouped by year.
    /// </summary>
    /// <remarks>
    ///   A year whose seasons hold no anime at all is left out.
    /// </remarks>
    /// <param name="options">The filters on the anime counted; the seasons and order are ignored.</param>
    /// <param name="includeImages">Whether to pick a poster and a backdrop for each season.</param>
    /// <returns>The years, newest first.</returns>
    IReadOnlyList<AnidbAnimeSeasonYear> GetSeasonsByYear(AnidbAnimeListOptions? options = null, bool includeImages = false);

    #endregion

    #region Days

    /// <summary>
    ///   Lists the airings of a range by local day, each day's airings of
    ///   one episode grouped under a lead.
    /// </summary>
    /// <remarks>
    ///   The airings are read through
    ///   <c>IAiringScheduleService.GetAiringsInRange</c> with the range in
    ///   <paramref name="timeZone"/>. A timed airing is placed at its slot
    ///   when that is in the range, else at the slot it was moved out of; a
    ///   date-only entry on its AniDB air date, when that day starts in the
    ///   range. Days with nothing on them are left out.
    /// </remarks>
    /// <param name="from">The start of the range, inclusive.</param>
    /// <param name="to">The end of the range, exclusive.</param>
    /// <param name="timeZone">The time zone the days are in, or <c>null</c> for UTC.</param>
    /// <param name="airingOptions">The airing filters.</param>
    /// <param name="everyChannel">Whether to keep each episode's other airings, or only its lead.</param>
    /// <exception cref="ArgumentException"><paramref name="to"/> is before <paramref name="from"/>.</exception>
    /// <returns>The days, in order.</returns>
    IReadOnlyList<AiringCalendarDay> GetCalendarDays(
        DateTimeOffset from,
        DateTimeOffset to,
        TimeZoneInfo? timeZone = null,
        EpisodeAiringFilteringOptions? airingOptions = null,
        bool everyChannel = true
    );

    #endregion
}
