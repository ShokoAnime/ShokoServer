using System;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   What AVDump knows of a video: the session dumping it, if any, and the
///   last dump that succeeded.
/// </summary>
public sealed record AnidbAvdumpVideoInfo
{
    /// <summary>
    ///   The queued or running session for the video, or <c>null</c> when
    ///   there is none.
    /// </summary>
    public IAnidbAvdumpSession? Session { get; init; }

    /// <summary>
    ///   When the video was last dumped successfully, in UTC, or <c>null</c>
    ///   when it never was.
    /// </summary>
    public DateTime? LastDumpedAt { get; init; }

    /// <summary>
    ///   The version of the AVDump component the last successful dump used,
    ///   or <c>null</c> when the video was never dumped.
    /// </summary>
    public string? LastVersion { get; init; }
}
