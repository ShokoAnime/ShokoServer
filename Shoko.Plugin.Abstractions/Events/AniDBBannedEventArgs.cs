using System;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when an AniDB ban is detected.
/// </summary>
public class AniDBBannedEventArgs : EventArgs
{
    /// <summary>
    /// Type of ban
    /// </summary>
    public AniDBBanType Type { get; }
    /// <summary>
    /// The time the ban occurred. It should be basically "now"
    /// </summary>
    public DateTime Time { get; }

    /// <summary>
    /// The time when Shoko will attempt again. This time is just guessed. We get no data or hint of any kind for this value to prevent additional bans.
    /// </summary>
    public DateTime ResumeTime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AniDBBannedEventArgs"/> class.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="time">The time the ban occurred.</param>
    /// <param name="resumeTime">The resume time.</param>
    public AniDBBannedEventArgs(AniDBBanType type, DateTime time, DateTime resumeTime)
    {
        Type = type;
        Time = time;
        ResumeTime = resumeTime;
    }
}

/// <summary>
/// Represents the type of AniDB ban.
/// </summary>
public enum AniDBBanType
{
    /// <summary>
    /// UDP ban.
    /// </summary>
    UDP,

    /// <summary>
    /// HTTP ban.
    /// </summary>
    HTTP,
}
