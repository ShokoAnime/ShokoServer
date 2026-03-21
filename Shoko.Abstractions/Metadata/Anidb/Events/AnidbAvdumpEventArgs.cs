using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Abstractions.Metadata.Anidb.Events;

/// <summary>
/// AniDB AVDump event arguments.
/// </summary>
public class AnidbAvdumpEventArgs : EventArgs
{
    /// <summary>
    /// The AVDump session id, if applicable to the event type.
    /// </summary>
    public int? SessionID { get; set; }

    /// <summary>
    /// The video id, if applicable to the event type.
    /// </summary>
    /// <value></value>
    public IReadOnlyList<int>? VideoIDs { get; set; }

    /// <summary>
    /// Absolute path of file being dumped.
    /// </summary>
    public IReadOnlyList<string>? AbsolutePaths { get; set; }

    /// <summary>
    /// The avdump event type. This is the most important property of the
    /// event, because it tells the event consumer which properties will be
    /// available.
    /// </summary>
    public AnidbAvdumpEventType Type { get; }

    /// <summary>
    /// The progress, this should be updated if it's sent with an event.
    /// </summary>
    public double? Progress { get; set; }

    /// <summary>
    /// Succeeded AniDB creq count.
    /// </summary>
    public int? SucceededCreqCount { get; set; }

    /// <summary>
    /// Failed AniDB creq count.
    /// </summary>
    public int? FailedCreqCount { get; set; }

    /// <summary>
    /// Pending AniDB creq count.
    /// </summary>
    public int? PendingCreqCount { get; set; }

    /// <summary>
    /// ED2K link, only sent with successful dump ended events.
    /// </summary>
    public IReadOnlyList<string>? ED2Ks { get; set; }

    /// <summary>
    /// The message for the event, if applicable.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// If a failure event occurs, then this property will contain the
    /// standard error.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The exception, if an install or generic exception event occurs.
    /// </summary>
    /// <value></value>
    public Exception? Exception { get; }

    /// <summary>
    /// When the AVDump session was started. Only sent in start, running and
    /// ending events.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the AVDump session ended. Only sent in ending events.
    /// </summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// Create a new AVDump event.
    /// </summary>
    /// <param name="messageType">The type of event.</param>
    /// <param name="message">The message.</param>
    public AnidbAvdumpEventArgs(AnidbAvdumpEventType messageType, string? message = null)
    {
        Type = messageType;
        Message = message;
    }

    /// <summary>
    /// Create a new AVDump event.
    /// </summary>
    /// <param name="messageType">The type of event.</param>
    /// <param name="ex">The exception.</param>
    public AnidbAvdumpEventArgs(AnidbAvdumpEventType messageType, Exception ex)
    {
        Type = messageType;
        Exception = ex;
    }
}
