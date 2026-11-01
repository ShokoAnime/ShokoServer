using System;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Video;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Models.AniDB;

public class AniDB_Message : IAnidbMessage
{
    /// <summary>
    ///   What the title of a "file moved" system message starts with, before
    ///   the file's ID.
    /// </summary>
    public const string FileMovedTitlePrefix = "file moved:";

    #region DB Columns

    public int AniDB_MessageID { get; set; }

    public int MessageID { get; set; }

    public int FromUserId { get; set; }

    public string FromUserName { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }

    public DateTime FetchedAt { get; set; }

    public AniDBMessageType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public AniDBMessageFlags Flags { get; set; }

    #endregion

    #region Flags

    public bool IsReadOnAniDB
    {
        get
        {
            return Flags.HasFlag(AniDBMessageFlags.ReadOnAniDB);
        }
        set
        {
            if (value)
                Flags |= AniDBMessageFlags.ReadOnAniDB;
            else
                Flags &= ~AniDBMessageFlags.ReadOnAniDB;
        }
    }

    public bool IsReadOnShoko
    {
        get
        {
            return Flags.HasFlag(AniDBMessageFlags.ReadOnShoko);
        }
        set
        {
            if (value)
                Flags |= AniDBMessageFlags.ReadOnShoko;
            else
                Flags &= ~AniDBMessageFlags.ReadOnShoko;
        }
    }

    public bool IsFileMoved
    {
        get
        {
            return Flags.HasFlag(AniDBMessageFlags.FileMoved);
        }
        set
        {
            if (value)
                Flags |= AniDBMessageFlags.FileMoved;
            else
                Flags &= ~AniDBMessageFlags.FileMoved;
        }
    }

    public bool IsFileMoveHandled
    {
        get
        {
            return Flags.HasFlag(AniDBMessageFlags.FileMoveHandled);
        }
        set
        {
            if (value)
                Flags |= AniDBMessageFlags.FileMoveHandled;
            else
                Flags &= ~AniDBMessageFlags.FileMoveHandled;
        }
    }

    public bool IsFileMoveHandledAutomatically
    {
        get
        {
            return Flags.HasFlag(AniDBMessageFlags.FileMoveHandledAutomatically);
        }
        set
        {
            if (value)
                Flags |= AniDBMessageFlags.FileMoveHandledAutomatically;
            else
                Flags &= ~AniDBMessageFlags.FileMoveHandledAutomatically;
        }
    }

    #endregion

    /// <summary>
    ///   The AniDB ID of the file a "file moved" message is about, read from
    ///   its title.
    /// </summary>
    public int? MovedFileID
        => IsFileMoved && Title.Length > FileMovedTitlePrefix.Length && int.TryParse(Title[FileMovedTitlePrefix.Length..].Trim(), out var fileID)
            ? fileID
            : null;

    #region IAnidbMessage Implementation

    int IAnidbMessage.ID => MessageID;

    AnidbMessageType IAnidbMessage.Type => (AnidbMessageType)Type;

    AnidbMessageKind IAnidbMessage.Kind => IsFileMoved ? AnidbMessageKind.FileMoved : AnidbMessageKind.Generic;

    int IAnidbMessage.FromUserID => FromUserId;

    bool IAnidbMessage.IsReadOnAnidb => IsReadOnAniDB;

    IVideo? IAnidbMessage.MovedVideo
        => MovedFileID is { } fileID && RepoFactory.StoredReleaseInfo.GetByReleaseURI($"{AnidbReleaseProvider.ReleasePrefix}{fileID}") is { } release
            ? RepoFactory.VideoLocal.GetByEd2k(release.ED2K)
            : null;

    bool IAnidbMessage.IsHandled => IsFileMoved && IsFileMoveHandled;

    bool IAnidbMessage.IsHandledAutomatically => IsFileMoved && IsFileMoveHandledAutomatically;

    #endregion
}
