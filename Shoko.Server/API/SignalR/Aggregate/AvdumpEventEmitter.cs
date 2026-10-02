using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Events;
using Shoko.Abstractions.User;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// Bridges the AVDump events to the aggregate hub. A session's events, and its
/// state on joining, reach the users who may see every video in the session;
/// events of no session, such as installing AVDump, reach everyone.
/// </summary>
/// <remarks>
/// A session that already ended, or a video that is gone, can't be checked,
/// and goes to users without restricted tags only.
/// </remarks>
public class AvdumpEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "avdump";

    private readonly VideoLocalRepository _videos;

    private readonly AniDB_AnimeRepository _anidbAnime;

    private readonly ILogger<AvdumpEventEmitter> _logger;

    public AvdumpEventEmitter(IHubContext<AggregateHub> hub, VideoLocalRepository videos, AniDB_AnimeRepository anidbAnime, ILogger<AvdumpEventEmitter> logger) : base(hub)
    {
        _videos = videos;
        _anidbAnime = anidbAnime;
        _logger = logger;
        ShokoEventHandler.Instance.AvdumpEvent += OnAVDumpEvent;
    }

    public void Dispose()
    {
        ShokoEventHandler.Instance.AvdumpEvent -= OnAVDumpEvent;
    }

    private async void OnAVDumpEvent(object? sender, AnidbAvdumpEventArgs eventArgs)
    {
        try
        {
            var mayReceive = eventArgs.SessionID is { } sessionID
                ? MaySeeVideos(() => eventArgs.VideoIDs ?? AVDumpHelper.GetActiveSessions().FirstOrDefault(session => session.SessionID == sessionID)?.VideoIDs)
                : (_ => true);
            await SendWhereAsync(mayReceive, "event", new AvdumpEventSignalRModel(eventArgs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'event' event.");
        }
    }

    protected override object[] GetInitialMessagesForUser(string connectionId, IUser user, DateTime? lastConnectedAt = null)
    {
        return [
            AVDumpHelper.GetActiveSessions()
                .Where(session => MaySeeVideos(() => session.VideoIDs)(user))
                .Select(session => new AvdumpEventSignalRModel(session))
                .ToList()
        ];
    }

    /// <summary>
    /// Whether a user may see every video in a session. The videos are looked
    /// up once, and only for a user with restricted tags.
    /// </summary>
    /// <param name="getVideoIDs">The session's videos, or <see langword="null"/> when the session is unknown.</param>
    /// <returns>The check.</returns>
    private Func<IUser, bool> MaySeeVideos(Func<IReadOnlyList<int>?> getVideoIDs)
    {
        EventAudience[]? audiences = null;
        return user => EventAudience.IsUnrestricted(user) || (audiences ??= GetAudiences(getVideoIDs())).All(audience => audience.IsVisibleTo(user));
    }

    /// <summary>
    /// Who may see each video of a session.
    /// </summary>
    /// <param name="videoIDs">The session's videos, or <see langword="null"/> when the session is unknown.</param>
    /// <returns>The audience of each video.</returns>
    private EventAudience[] GetAudiences(IReadOnlyList<int>? videoIDs)
    {
        if (videoIDs is null)
            return [EventAudience.Unrestricted];

        return [.. videoIDs.ToArray().Select(videoID => _videos.GetByID(videoID) is { } video ? EventAudience.ForVideo(video, _anidbAnime.GetByAnimeID) : EventAudience.Unrestricted)];
    }
}
