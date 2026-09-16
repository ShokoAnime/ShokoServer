using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Data transfer object (DTO) for updating an existing airing schedule with
///   support for partial updates. A property left alone is not touched, and a
///   property where <c>null</c> is itself a value carries a
///   <c>Has…Set</c> flag, set by its setter.
/// </summary>
/// <remarks>
///   A schedule's series, season, key and channel cannot be updated. Changing
///   any of them means a different schedule.
/// </remarks>
public sealed class AiringScheduleUpdateData
{
    /// <summary>
    ///   What the schedule releases. Replaces the whole set when given, and
    ///   leaves it alone when <c>null</c>. Track changes are plain updates and
    ///   never count as a delay.
    /// </summary>
    public IReadOnlyList<AiringTrackData>? Tracks { get; set; }

    /// <summary>
    ///   Used by the service to determine whether
    ///   <see cref="FirstEpisodeNumber"/> should be updated. Set to <c>true</c>
    ///   when the property is set.
    /// </summary>
    public bool HasFirstEpisodeNumberSet { get; private set; }

    private int? _firstEpisodeNumber;

    /// <summary>
    ///   The first episode the schedule covers, in the series' (or season's)
    ///   numbering. Set it to <c>null</c> to leave the range open-ended.
    /// </summary>
    public int? FirstEpisodeNumber
    {
        get => _firstEpisodeNumber;
        set
        {
            HasFirstEpisodeNumberSet = true;
            _firstEpisodeNumber = value;
        }
    }

    /// <summary>
    ///   Used by the service to determine whether
    ///   <see cref="LastEpisodeNumber"/> should be updated. Set to <c>true</c>
    ///   when the property is set.
    /// </summary>
    public bool HasLastEpisodeNumberSet { get; private set; }

    private int? _lastEpisodeNumber;

    /// <summary>
    ///   The last episode the schedule covers, in the series' (or season's)
    ///   numbering. Set it to <c>null</c> to leave the range open-ended.
    /// </summary>
    public int? LastEpisodeNumber
    {
        get => _lastEpisodeNumber;
        set
        {
            HasLastEpisodeNumberSet = true;
            _lastEpisodeNumber = value;
        }
    }

    /// <summary>
    ///   Whether the provider considers the run finished. <c>null</c> leaves it
    ///   alone.
    /// </summary>
    public bool? IsFinished { get; set; }

    /// <summary>
    ///   Used by the service to determine whether <see cref="TimeZone"/> should
    ///   be updated. Set to <c>true</c> when the property is set.
    /// </summary>
    public bool HasTimeZoneSet { get; private set; }

    private TimeZoneInfo? _timeZone;

    /// <summary>
    ///   The time zone the source's own times are in, for display only. Set it
    ///   to <c>null</c> to clear it.
    /// </summary>
    public TimeZoneInfo? TimeZone
    {
        get => _timeZone;
        set
        {
            HasTimeZoneSet = true;
            _timeZone = value;
        }
    }

    /// <summary>
    ///   Used by the service to determine whether <see cref="Url"/> should be
    ///   updated. Set to <c>true</c> when the property is set.
    /// </summary>
    public bool HasUrlSet { get; private set; }

    private string? _url;

    /// <summary>
    ///   The series' page on the channel. Set it to <c>null</c> to clear it.
    /// </summary>
    public string? Url
    {
        get => _url;
        set
        {
            HasUrlSet = true;
            _url = value;
        }
    }
}
