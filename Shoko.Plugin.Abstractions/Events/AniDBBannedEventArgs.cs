using System;

#nullable enable
namespace Shoko.Plugin.Abstractions;

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

    public AniDBBannedEventArgs(AniDBBanType type, DateTime time, DateTime resumeTime)
    {
        Type = type;
        Time = time;
        ResumeTime = resumeTime;
    }
}

public enum AniDBBanType
{
    UDP,
    HTTP,
}
