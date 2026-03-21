
namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
/// The type of AVDump event.
/// </summary>
public enum AnidbAvdumpEventType
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
    /// A generic .NET exception occurred while trying to run the AVDump
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
    /// A generic .NET exception occurred while trying to run the AVDump
    /// session and the session have ended as a result.
    /// </summary>
    InstallException,
}
