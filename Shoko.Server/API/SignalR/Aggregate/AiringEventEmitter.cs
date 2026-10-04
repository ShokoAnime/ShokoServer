using System;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// Bridges the airing schedule service's events to the aggregate hub.
/// </summary>
/// <remarks>
/// This is its own feed rather than a few more messages on the metadata one:
/// every emitter here wraps exactly one service, and a client interested in
/// when things air is rarely the same client interested in series and episode
/// metadata changing. Keeping them apart lets it subscribe to the one it wants.
/// Each user gets the airings it may see, by the rule of the airing calendar:
/// an airing whose anime is unknown is shown to everyone. A sweep names no
/// series, and goes to everyone.
/// </remarks>
public class AiringEventEmitter : BaseEventEmitter, IDisposable
{
    public override string Name => "airing";

    private readonly ILogger<AiringEventEmitter> _logger;

    /// <summary>
    /// The airing subscription this emitter holds for as long as it lives. Core
    /// takes no filters: every hub client gets the whole minute and filters
    /// client-side, the way it always has.
    /// </summary>
    private readonly IDisposable _subscription;

    /// <summary>
    /// The service the sweep handler is detached from on dispose, since that
    /// one is a plain event rather than a subscription.
    /// </summary>
    private readonly IAiringScheduleService _airingScheduleService;

    private readonly AniDB_AnimeRepository _anidbAnime;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringEventEmitter"/> class.
    /// </summary>
    /// <param name="hub">The aggregate hub.</param>
    /// <param name="airingScheduleService">The airing schedule service.</param>
    /// <param name="anidbAnime">The AniDB anime, to check airings against.</param>
    /// <param name="logger">The logger.</param>
    public AiringEventEmitter(IHubContext<AggregateHub> hub, IAiringScheduleService airingScheduleService, AniDB_AnimeRepository anidbAnime, ILogger<AiringEventEmitter> logger) : base(hub)
    {
        ArgumentNullException.ThrowIfNull(airingScheduleService);

        _logger = logger;
        _airingScheduleService = airingScheduleService;
        _anidbAnime = anidbAnime;
        _subscription = airingScheduleService.SubscribeToAirings(OnEpisodesAired);
        _airingScheduleService.SweepCompleted += OnSweepCompleted;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _airingScheduleService.SweepCompleted -= OnSweepCompleted;
        _subscription.Dispose();
        GC.SuppressFinalize(this);
    }

    private async void OnEpisodesAired(EpisodeAiredEventArgs e)
    {
        try
        {
            await SendVisiblePartsAsync(
                "episode.aired",
                e.Airings,
                [.. e.Airings.Select(airing => EventAudience.ForAiring(AnimeIDOf(airing), _anidbAnime.GetByAnimeID))],
                airings => new EpisodeAiredSignalRModel(e.AiredAt, airings)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'episode.aired' event.");
        }
    }

    private async void OnSweepCompleted(object? sender, AiringScheduleSweepEventArgs e)
    {
        try
        {
            await SendAsync("provider.swept", new AiringSweepCompletedSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'provider.swept' event.");
        }
    }

    /// <summary>
    /// The AniDB anime of an airing's series, found the way the airing calendar
    /// finds it.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The AniDB anime ID, or <c>null</c> when the series is of another source or unknown.</returns>
    private static int? AnimeIDOf(IEpisodeAiring airing)
        => ((ISeries?)airing.ShokoEpisode?.Series ?? airing.AnidbEpisode?.Series ?? airing.Episode?.Series ?? airing.Schedule?.Series) switch
        {
            IShokoSeries series => series.AnidbAnimeID,
            IAnidbAnime anime => anime.AnidbID,
            _ => null,
        };
}
