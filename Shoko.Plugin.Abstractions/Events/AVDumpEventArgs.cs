
using System;
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions
{
    public class AVDumpEventArgs : EventArgs
    {
        /// <summary>
        /// The AVDump session id, if applicable to the event type.
        /// </summary>
        public int? SessionID { get; set; }

        /// <summary>
        /// The command request id, if applicable to the event type.
        /// </summary>
        /// <value></value>
        public int? CommandID { get; set; }

        /// <summary>
        /// The video id, if applicable to the event type.
        /// </summary>
        /// <value></value>
        public IReadOnlyList<int> VideoIDs { get; set; }

        /// <summary>
        /// Absolute path of file being dumped.
        /// </summary>
        public IReadOnlyList<string> AbsolutePaths { get; set; }

        /// <summary>
        /// The avdump event type. This is the most important property of the
        /// event, because it tells the event consumer which properties will be
        /// available.
        /// </summary>
        public AVDumpEventType Type { get; }

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
        public IReadOnlyList<string> ED2Ks { get; set; }

        /// <summary>
        /// The message for the event, if applicable.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// If a failure event occurs, then this property will contain the
        /// standard error.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// The exception, if an install or generic exception event occurs.
        /// </summary>
        /// <value></value>
        public Exception Exception { get; }

        /// <summary>
        /// When the AVDump session was started. Only sent in start, running and
        /// ending events.
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// When the AVDump session ended. Only sent in ending events.
        /// </summary>
        public DateTime? EndedAt { get; set; }

        public AVDumpEventArgs(AVDumpEventType messageType, string message = null)
        {
            Type = messageType;
            Message = message;
        }

        public AVDumpEventArgs(AVDumpEventType messageType, Exception ex)
        {
            Type = messageType;
            Exception = ex;
        }
    }

    public enum AVDumpEventType
    {
        /// <summary>
        /// Any message sent to the standard output from the avdump binary that's
        /// not a progress message.
        /// </summary>
        Message = 0,

        /// <summary>
        /// Any message sent to the standard error from the avdump binary.
        /// </summary>
        Error,

        /// <summary>
        /// A progress update sent to the standard output from the avdump
        /// binary. Will contain an updated progress.
        /// </summary>
        Progress,

        /// <summary>
        /// AniDB Creq count update.
        /// </summary>
        CreqUpdate,

        /// <summary>
        /// A file has been processed and a link is available.
        /// </summary>
        ED2KLink,

        /// <summary>
        /// A message indicating an AVDump session have started for a given
        /// file and/or command request.
        /// </summary>
        Started,

        /// <summary>
        /// A message indicating an AVDump session have ended with a success for
        /// a given file and/or command request.
        /// </summary>
        Success,

        /// <summary>
        /// A message indicating an AVDump session have ended with a failure for
        /// a given file and/or command request.
        /// </summary>
        Failure,

        /// <summary>
        /// A message sent for any running sessions to new SignalR clients.
        /// </summary>
        Restore,

        /// <summary>
        /// A generic .NET exception occured while trying to run the AVDump
        /// session and the session have ended as a result.
        /// </summary>
        GenericException,

        /// <summary>
        /// The UDP AVDump Api Key is missing from the settings.
        /// </summary>
        MissingApiKey,

        /// <summary>
        /// Unable to authenticate with Anidb.
        /// </summary>
        InvalidCredentials,

        /// <summary>
        /// All attempts at communicating with Anidb failed because we received
        /// no reply before the timeout was reached.
        /// </summary>
        Timeout,

        /// <summary>
        /// A message indicating we're trying to install AVDump before starting
        /// the AVDump session.
        /// </summary>
        InstallingAVDump,

        /// <summary>
        /// A message indicating we're trying to install AVDump before starting
        /// the AVDump session.
        /// </summary>
        InstalledAVDump,

        /// <summary>
        /// A generic .NET exception occured while trying to run the AVDump
        /// session and the session have ended as a result.
        /// </summary>
        InstallException,
    }
}
