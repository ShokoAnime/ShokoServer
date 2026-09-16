using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// Bridges the airing schedule service's events to the aggregate hub.
/// </summary>
/// <remarks>
/// This is its own feed rather than a few more messages on the metadata one:
/// every emitter here wraps exactly one service, and a client interested in
/// when things air is rarely the same client interested in series and episode
/// metadata changing. Keeping them apart lets it subscribe to the one it wants.
/// </remarks>
public class AiringEventEmitter : BaseEventEmitter, IDisposable
{
    private readonly ILogger<AiringEventEmitter> _logger;

    /// <summary>
    /// The airing subscription this emitter holds for as long as it lives. Core
    /// takes no filters: every hub client gets the whole minute and filters
    /// client-side, the way it always has.
    /// </summary>
    private readonly IDisposable _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringEventEmitter"/> class.
    /// </summary>
    /// <param name="hub">The aggregate hub.</param>
    /// <param name="airingScheduleService">The airing schedule service.</param>
    /// <param name="logger">The logger.</param>
    public AiringEventEmitter(IHubContext<AggregateHub> hub, IAiringScheduleService airingScheduleService, ILogger<AiringEventEmitter> logger) : base(hub)
    {
        ArgumentNullException.ThrowIfNull(airingScheduleService);

        _logger = logger;
        _subscription = airingScheduleService.SubscribeToAirings(OnEpisodesAired);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscription.Dispose();
        GC.SuppressFinalize(this);
    }

    private async void OnEpisodesAired(EpisodeAiredEventArgs e)
    {
        try
        {
            await SendAsync("episode.aired", new EpisodeAiredSignalRModel(e));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while sending the 'episode.aired' event.");
        }
    }
}
