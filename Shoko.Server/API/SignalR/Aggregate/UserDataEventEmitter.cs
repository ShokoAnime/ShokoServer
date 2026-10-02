using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// Bridges the user data events to the aggregate hub. A user's data reaches
/// that user always, and the admins who may see what the data is about.
/// </summary>
/// <remarks>
/// A user's own data is never kept from them by their restricted tags. No user
/// data event is a removal, so an admin is checked against its entity.
/// </remarks>
public class UserDataEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly IUserDataService _userDataService;

    private readonly AniDB_AnimeRepository _anidbAnime;

    private readonly ILogger<UserDataEventEmitter> _logger;

    public override string Name => "userData";

    public UserDataEventEmitter(IHubContext<AggregateHub> hub, IUserDataService userDataService, AniDB_AnimeRepository anidbAnime, ILogger<UserDataEventEmitter> logger) : base(hub)
    {
        _userDataService = userDataService;
        _anidbAnime = anidbAnime;
        _logger = logger;
        _userDataService.VideoUserDataSaved += OnVideoUserDataSaved;
        _userDataService.EpisodeUserDataSaved += OnEpisodeUserDataSaved;
        _userDataService.SeriesUserDataSaved += OnSeriesUserDataSaved;
        _userDataService.GroupUserDataSaved += OnGroupUserDataSaved;
    }

    public void Dispose()
    {
        _userDataService.VideoUserDataSaved -= OnVideoUserDataSaved;
        _userDataService.EpisodeUserDataSaved -= OnEpisodeUserDataSaved;
        _userDataService.SeriesUserDataSaved -= OnSeriesUserDataSaved;
        _userDataService.GroupUserDataSaved -= OnGroupUserDataSaved;
    }

    private async void OnVideoUserDataSaved(object? sender, VideoUserDataSavedEventArgs e)
    {
        try
        {
            await SendToOwnerAsync(e.User, EventAudience.ForVideo(e.Video, _anidbAnime.GetByAnimeID), "video.saved", new VideoUserDataSavedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'video.saved' event.");
        }
    }

    private async void OnEpisodeUserDataSaved(object? sender, EpisodeUserDataSavedEventArgs e)
    {
        try
        {
            await SendToOwnerAsync(e.User, EventAudience.ForEpisode(e.Episode, _anidbAnime.GetByAnimeID), "episode.saved", new EpisodeUserDataSavedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'episode.saved' event.");
        }
    }

    private async void OnSeriesUserDataSaved(object? sender, SeriesUserDataSavedEventArgs e)
    {
        try
        {
            await SendToOwnerAsync(e.User, EventAudience.ForSeries(e.Series, _anidbAnime.GetByAnimeID), "series.saved", new SeriesUserDataSavedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'series.saved' event.");
        }
    }

    private async void OnGroupUserDataSaved(object? sender, GroupUserDataSavedEventArgs e)
    {
        try
        {
            await SendToOwnerAsync(e.User, EventAudience.ForGroup(e.Group, _anidbAnime.GetByAnimeID), "group.saved", new GroupUserDataSavedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'group.saved' event.");
        }
    }

    /// <summary>
    /// Sends a message about a user's data to that user, and to the admins who
    /// may see what the data is about.
    /// </summary>
    /// <param name="owner">The user the data belongs to.</param>
    /// <param name="audience">Who may see what the data is about, asked for admins only.</param>
    /// <param name="subject">The message's subject.</param>
    /// <param name="model">The message.</param>
    /// <returns>A task completing once the message was handed to the connections.</returns>
    private Task SendToOwnerAsync(IUser owner, EventAudience audience, string subject, object model)
        => SendWhereAsync(user => user.LocalID == owner.LocalID || (user.IsAdmin && audience.IsVisibleTo(user)), subject, model);
}
