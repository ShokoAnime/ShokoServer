using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// One day of the calendar, in the requested time zone, with what airs on it.
/// </summary>
public class AiringCalendarDay
{
    /// <summary>
    /// The local date.
    /// </summary>
    [Required]
    public required DateOnly Date { get; init; }

    /// <summary>
    /// The day's episodes, ordered by their lead airings: the date-only
    /// entries first, then by time.
    /// </summary>
    [Required]
    public required List<Episode> Episodes { get; init; }

    #region Nested Types

    /// <summary>
    /// The airings of one episode on the day, shown together. An airing of an
    /// unknown episode stands alone.
    /// </summary>
    public class Episode
    {
        /// <summary>
        /// The airing shown first: the preferred one, else the first timed
        /// one, else the date-only one.
        /// </summary>
        [Required]
        public required Entry Lead { get; init; }

        /// <summary>
        /// The episode's other airings that day, in the day's order; empty
        /// without <c>everyChannel</c>.
        /// </summary>
        [Required]
        public required List<Entry> Others { get; init; }
    }

    /// <summary>
    /// An airing placed on the day.
    /// </summary>
    public class Entry
    {
        /// <summary>
        /// The airing, in the shape of the airings endpoint.
        /// </summary>
        [Required]
        public required EpisodeAiring Airing { get; init; }

        /// <summary>
        /// When it is shown, with the time zone's offset: its slot, or the
        /// slot it was moved out of when only that falls in the range, or the
        /// start of its day for a date-only entry.
        /// </summary>
        [Required]
        public required DateTimeOffset Time { get; init; }

        /// <summary>
        /// Whether it is a date-only entry, shown for the whole day.
        /// </summary>
        [Required]
        public required bool IsAllDay { get; init; }

        /// <summary>
        /// The slot it was moved out of, when it is shown at its new slot
        /// after a delay.
        /// </summary>
        public required DateTimeOffset? MovedFrom { get; init; }

        /// <summary>
        /// The slot it was moved to, when it is shown at the slot it was moved
        /// out of.
        /// </summary>
        public required DateTimeOffset? MovedTo { get; init; }
    }

    #endregion
}
