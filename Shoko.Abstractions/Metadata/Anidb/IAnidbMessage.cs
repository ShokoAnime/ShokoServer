using System;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
///   A message from the AniDB user's inbox, as the server fetched it.
/// </summary>
public interface IAnidbMessage
{
    /// <summary>
    ///   AniDB's ID for the message.
    /// </summary>
    int ID { get; }

    /// <summary>
    ///   Who sent it, as AniDB marks it.
    /// </summary>
    AnidbMessageType Type { get; }

    /// <summary>
    ///   What it is about, as far as the server can act on it.
    /// </summary>
    AnidbMessageKind Kind { get; }

    /// <summary>
    ///   The AniDB ID of the user who sent it.
    /// </summary>
    int FromUserID { get; }

    /// <summary>
    ///   The name of the user who sent it.
    /// </summary>
    string FromUserName { get; }

    /// <summary>
    ///   The title.
    /// </summary>
    string Title { get; }

    /// <summary>
    ///   The body, as AniDB sent it.
    /// </summary>
    string Body { get; }

    /// <summary>
    ///   When it was sent.
    /// </summary>
    DateTime SentAt { get; }

    /// <summary>
    ///   When the server fetched it.
    /// </summary>
    DateTime FetchedAt { get; }

    /// <summary>
    ///   Whether the server acknowledged it to AniDB, marking it read there.
    /// </summary>
    bool IsReadOnAnidb { get; }

    /// <summary>
    ///   Whether it was marked read on this server.
    /// </summary>
    bool IsReadOnShoko { get; }

    /// <summary>
    ///   For a <see cref="AnidbMessageKind.FileMoved"/> message, the AniDB ID
    ///   of the file that was moved; otherwise <c>null</c>.
    /// </summary>
    int? MovedFileID { get; }

    /// <summary>
    ///   For a <see cref="AnidbMessageKind.FileMoved"/> message, the video in
    ///   the collection the moved file is, if any; otherwise <c>null</c>.
    /// </summary>
    IVideo? MovedVideo { get; }

    /// <summary>
    ///   Whether the message was acted on. Always <c>false</c> for a kind the
    ///   server has no handling for.
    /// </summary>
    bool IsHandled { get; }

    /// <summary>
    ///   Whether it was acted on without anyone asking, because handling
    ///   such messages automatically is turned on.
    /// </summary>
    bool IsHandledAutomatically { get; }
}
