using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Events;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// Bridges the release events to the aggregate hub. A release event reaches
/// the users who may see the video, counting the anime of the release too.
/// </summary>
/// <remarks>
/// A deleted release is checked against the anime it was linked to and those
/// of the release replacing it, if any. When it was linked but none of its
/// anime is known, it goes to users without restricted tags only.
/// </remarks>
public class ReleaseEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "release";

    private readonly IVideoReleaseService _videoService;

    private readonly AniDB_AnimeRepository _anidbAnime;

    private readonly AniDB_EpisodeRepository _anidbEpisodes;

    private readonly ILogger<ReleaseEventEmitter> _logger;

    public ReleaseEventEmitter(
        IHubContext<AggregateHub> hub,
        IVideoReleaseService videoReleaseService,
        AniDB_AnimeRepository anidbAnime,
        AniDB_EpisodeRepository anidbEpisodes,
        ILogger<ReleaseEventEmitter> logger
    ) : base(hub)
    {
        _videoService = videoReleaseService;
        _anidbAnime = anidbAnime;
        _anidbEpisodes = anidbEpisodes;
        _logger = logger;
        _videoService.ReleaseSaved += OnReleaseSaved;
        _videoService.ReleaseDeleted += OnReleaseDeleted;
        _videoService.SearchCompleted += OnSearchCompleted;
    }

    public void Dispose()
    {
        _videoService.ReleaseSaved -= OnReleaseSaved;
        _videoService.ReleaseDeleted -= OnReleaseDeleted;
        _videoService.SearchCompleted -= OnSearchCompleted;
    }

    private async void OnReleaseSaved(object? sender, VideoReleaseSavedEventArgs e)
    {
        try
        {
            var audience = EventAudience.ForVideo(e.Video, _anidbAnime.GetByAnimeID, () => AnimeIDsOf(e.ReleaseInfo));
            await SendToAudienceAsync(audience, "saved", new VideoReleaseSavedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'saved' event.");
        }
    }

    private async void OnReleaseDeleted(object? sender, VideoReleaseDeletedEventArgs e)
    {
        try
        {
            var audience = EventAudience.ForVideo(e.Video, _anidbAnime.GetByAnimeID, () => AnimeIDsOf(e.ReleaseInfo).Concat(AnimeIDsOf(e.NewReleaseInfo)), isRemoval: true);
            await SendToAudienceAsync(audience, "removed", new ReleaseDeletedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'removed' event.");
        }
    }

    private async void OnSearchCompleted(object? sender, VideoReleaseSearchCompletedEventArgs e)
    {
        try
        {
            var audience = EventAudience.ForVideo(e.Video, _anidbAnime.GetByAnimeID, () => AnimeIDsOf(e.ReleaseInfo));
            await SendToAudienceAsync(audience, "search.completed", new VideoReleaseSearchCompletedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'search.completed' event.");
        }
    }

    /// <summary>
    /// The AniDB anime a release is linked to, through the episode where the
    /// cross-reference names no anime.
    /// </summary>
    /// <param name="release">The release, if any.</param>
    /// <returns>The AniDB anime IDs.</returns>
    private IEnumerable<int> AnimeIDsOf(IReleaseInfo? release)
        => release?.CrossReferences.Select(xref => xref.AnidbAnimeID ?? _anidbEpisodes.GetByEpisodeID(xref.AnidbEpisodeID)?.AnimeID ?? 0) ?? [];
}
