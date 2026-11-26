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
    ///   anime itself outside the collection, and count from the airing
    ///   options' <see cref="EpisodeAiringFilteringOptions.At"/>, or now: an
    ///   episode on air then is the next airing until its slot ends. An anime
    ///   has finished when its end date passed by that time's UTC date. With a
    ///   layout in the anime options'
    ///   <see cref="AnidbAnimeListOptions.SeasonSections"/>, only the anime a
    ///   section of it takes in the season are listed.
    /// </remarks>
    /// <param name="year">The year.</param>
    /// <param name="season">The season.</param>
    /// <param name="animeOptions">The anime filters and order; its seasons and reference time are replaced by this read's.</param>
    /// <param name="airingOptions">The airing filters and the reference time; the read is always next-only.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="year"/> is not between 1 and 9999.</exception>
    /// <exception cref="ArgumentNullException">
    ///   A filter in the options depends on the user and the options name none.
    /// </exception>
    /// <returns>The anime.</returns>
    IReadOnlyList<SeasonAnimeEntry> GetSeasonAnime(
        int year,
        YearlySeason season,
        AnidbAnimeListOptions? animeOptions = null,
        EpisodeAiringFilteringOptions? airingOptions = null
    );

    /// <summary>
    ///   Lists the cached AniDB anime of a yearly season by section, as
    ///   <see cref="GetSeasonAnime"/> reads them.
    /// </summary>
    /// <remarks>
    ///   Each anime goes to the first section that takes it, an anime no
    ///   section takes is left out, and empty sections are kept. Each
    ///   section is sorted by next airing, or keeps the anime's order when
    ///   the anime filter has a sorting expression.
    /// </remarks>
    /// <param name="year">The year.</param>
    /// <param name="season">The season.</param>
    /// <param name="sections">
    ///   The layout, or <c>null</c> for the anime options'
    ///   <see cref="AnidbAnimeListOptions.SeasonSections"/>, or else
    ///   <see cref="SeasonSectionDefinition.DefaultLayout"/>.
    /// </param>
    /// <param name="animeOptions">The anime filters; its seasons, layout and reference time are replaced by this read's.</param>
    /// <param name="airingOptions">The airing filters and the reference time; the read is always next-only.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="year"/> is not between 1 and 9999.</exception>
    /// <exception cref="ArgumentException"><paramref name="sections"/> holds a <c>null</c> section.</exception>
    /// <exception cref="ArgumentNullException">
    ///   A filter in the options depends on the user and the options name none.
    /// </exception>
    /// <returns>Every section of the layout, in its order.</returns>
    IReadOnlyList<SeasonSection> GetSeasonSections(
        int year,
        YearlySeason season,
        IReadOnlyList<SeasonSectionDefinition>? sections = null,
        AnidbAnimeListOptions? animeOptions = null,
        EpisodeAiringFilteringOptions? airingOptions = null
    );

    /// <summary>
    ///   Lists the seasons the cached AniDB anime are in, as
    ///   <c>IAnidbService.GetCachedAnimeSeasons</c> does, grouped by year.
    /// </summary>
    /// <remarks>
    ///   A year whose seasons hold no anime at all is left out. The season
    ///   under way is the one as of the options'
    ///   <see cref="AnidbAnimeListOptions.At"/>, or now.
    /// </remarks>
    /// <param name="options">The filters on the anime counted and the reference time; the seasons and order are ignored.</param>
    /// <param name="includeImages">Whether to pick a poster and a backdrop for each season.</param>
    /// <exception cref="ArgumentNullException">
    ///   A filter in the options depends on the user and the options name none.
    /// </exception>
    /// <returns>The years, newest first.</returns>
    IReadOnlyList<AnidbAnimeSeasonYear> GetSeasonsByYear(AnidbAnimeListOptions? options = null, bool includeImages = false);

    /// <summary>
    ///   The yearly season a day falls in, with its bounds, by the rule the
    ///   calendar starts entries by.
    /// </summary>
    /// <remarks>
    ///   With a type, a season starts on the Monday of the week holding its
    ///   first day (1 January, April, July or October), less the type's
    ///   lead-in: four weeks for TV series and TV shorts, one week for every
    ///   other type. That is the season an entry of that type starting on the
    ///   day is filed in. Without a type, the season is the plain calendar
    ///   quarter, which is how an entry's seasons after its first are counted.
    /// </remarks>
    /// <param name="date">The day.</param>
    /// <param name="type">The type whose lead-in applies, or <c>null</c> for calendar quarters.</param>
    /// <returns>The season and its bounds.</returns>
    SeasonBounds GetSeasonBounds(DateOnly date, AnimeType? type = null);

    /// <summary>
    ///   The days a yearly season runs, by the same rule as
    ///   <see cref="GetSeasonBounds(DateOnly, AnimeType?)"/>.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="season">The season.</param>
    /// <param name="type">The type whose lead-in applies, or <c>null</c> for calendar quarters.</param>
    /// <returns>The season and its bounds. The last season of 9999 runs to <see cref="DateOnly.MaxValue"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="year"/> is not between 1 and 9999.</exception>
    SeasonBounds GetSeasonBounds(int year, YearlySeason season, AnimeType? type = null);

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
    /// <exception cref="ArgumentNullException">
    ///   A filter in the options depends on the user and the options name none.
    /// </exception>
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
