using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.v3.Models.Airing;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// Turns the airing schedule into a live signal: it keeps the next hour of
/// airings in memory and raises <see cref="IAiringScheduleService.EpisodeAired"/>
/// as each slot passes, so plugins, core and SignalR clients can react to an
/// episode airing without polling the read path.
/// </summary>
/// <remarks>
///   <para>
///     This is an <see cref="IHostedService"/> rather than a recurring queue
///     job because the event has to land within a minute of the slot. A
///     recurring job is dispatched through the worker pool, where it queues
///     behind hashing, AniDB and TMDB work and can be held back by an
///     acquisition filter for as long as that filter says no — fine for a daily
///     sweep, useless for a clock. It also owns in-memory state (the horizon
///     and the watermark), which a job, being constructed per execution, has
///     nowhere to keep.
///   </para>
///   <para>
///     The horizon is short on purpose. Estimates are computed rather than
///     stored, so filling it means running the estimate pipeline over the
///     window; an hour of lookahead keeps that cheap while leaving plenty of
///     slack between rebuilds.
///   </para>
/// </remarks>
public sealed class EpisodeAiringNotificationService : BackgroundService
{
    /// <summary>
    /// How far ahead the horizon reaches. Everything in it is held in memory
    /// until its slot passes or a rebuild replaces it.
    /// </summary>
    internal static readonly TimeSpan HorizonLength = TimeSpan.FromHours(1);

    /// <summary>
    /// How often the horizon is rebuilt even when nothing changed, which is
    /// what keeps its tail from running out. Anything shorter pays for the
    /// estimate pipeline more often than the lookahead needs.
    /// </summary>
    internal static readonly TimeSpan HorizonRefreshInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How often the loop wakes. It does not tick on this interval: it checks
    /// the wall clock and does its work once per minute boundary, so the
    /// schedule never drifts away from the minute the way a timer started at
    /// process boot would.
    /// </summary>
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How often the watermark is written when nothing fired. A minute that
    /// raised events persists immediately; the quiet ones are batched, because
    /// the row exists to be read once at boot and to be looked at by a human.
    /// </summary>
    internal static readonly TimeSpan WatermarkFlushInterval = TimeSpan.FromMinutes(5);

    private readonly ILogger<EpisodeAiringNotificationService> _logger;

    private readonly AiringScheduleService _service;

    private readonly ISystemService _systemService;

    /// <summary>
    /// The upcoming airings, keyed by airing ID. An episode on three channels
    /// is three entries; one airing resolved for two linked episodes is still
    /// one, which is what makes the event per-airing rather than per-episode.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, HorizonEntry> _horizon = [];

    private int _horizonStale;

    private DateTime _horizonBuiltAt = DateTime.MinValue;

    private DateTime _watermark = DateTime.MinValue;

    private DateTime _watermarkPersistedAt = DateTime.MinValue;

    private DateTime _lastProcessedMinute = DateTime.MinValue;

    private bool _started;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeAiringNotificationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="airingScheduleService">The airing schedule service that owns the event and the read path.</param>
    /// <param name="systemService">The system service, for the "is the server actually up" gate.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public EpisodeAiringNotificationService(
        ILogger<EpisodeAiringNotificationService> logger,
        IAiringScheduleService airingScheduleService,
        ISystemService systemService
    )
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(airingScheduleService);
        ArgumentNullException.ThrowIfNull(systemService);

        _logger = logger;
        _service = (AiringScheduleService)airingScheduleService;
        _systemService = systemService;
        _service.ScheduleUpdated += OnScheduleUpdated;
        _service.AiringsUpdated += OnAiringsUpdated;
        _service.ProvidersUpdated += OnProvidersUpdated;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _service.ScheduleUpdated -= OnScheduleUpdated;
        _service.AiringsUpdated -= OnAiringsUpdated;
        _service.ProvidersUpdated -= OnProvidersUpdated;
        base.Dispose();
    }

    #region Loop

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            // The host starts before the database migrations and the plugin
            // parts do, and a server in setup mode has neither, so the loop
            // idles until the server says it is up.
            if (!_systemService.IsStarted || _systemService.InSetupMode || _systemService.IsDatabaseBlocked || !_service.HasParts)
                continue;

            try
            {
                Tick(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while dispatching episode airing notifications.");
            }
        }
    }

    /// <summary>
    /// Does one minute's worth of work: pick up the watermark on the first
    /// pass, rebuild the horizon when it is stale or stale-by-age, then raise
    /// the event for everything whose slot has passed.
    /// </summary>
    /// <remarks>
    /// The loop wakes twice a minute but this returns immediately until the
    /// wall-clock minute changes, so the work is pinned to minute boundaries
    /// and an airing is never raised early — at worst it is raised within one
    /// poll of its slot.
    /// </remarks>
    /// <param name="nowUtc">The current time, in UTC.</param>
    /// <returns>The airings whose slot passed, in slot order, which is what the tests assert on.</returns>
    internal IReadOnlyList<IEpisodeAiring> Tick(DateTime nowUtc)
    {
        var minute = TruncateToMinute(nowUtc);
        if (minute <= _lastProcessedMinute)
            return [];

        _lastProcessedMinute = minute;
        if (!_started)
            SkipForward(minute);

        if (Interlocked.Exchange(ref _horizonStale, 0) is not 0 || minute - _horizonBuiltAt >= HorizonRefreshInterval)
            RebuildHorizon(minute);

        var due = _horizon.Values
            .Where(entry => entry.AiredAt > _watermark && entry.AiredAt <= minute)
            .OrderBy(entry => entry.AiredAt)
            .ThenBy(entry => entry.Airing.Key, StringComparer.Ordinal)
            .ToList();
        foreach (var entry in due)
        {
            // Dropping it here is belt and braces: the watermark below already
            // stops a rebuild from handing the same slot back a second time.
            _horizon.TryRemove(entry.Airing.ID, out _);
            try
            {
                _service.RaiseEpisodeAired(entry.Airing, entry.AiredAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A handler threw while being told that airing {AiringID} aired.", entry.Airing.ID);
            }
        }

        _watermark = minute;
        if (due.Count > 0 || minute - _watermarkPersistedAt >= WatermarkFlushInterval)
            SaveWatermark(minute);

        return [.. due.Select(entry => entry.Airing)];
    }

    #endregion

    #region Horizon

    /// <summary>
    /// The airings currently held in memory, for tests and for anything that
    /// wants to see what the ticker is waiting on.
    /// </summary>
    internal IReadOnlyList<IEpisodeAiring> Horizon
        => [.. _horizon.Values.OrderBy(entry => entry.AiredAt).Select(entry => entry.Airing)];

    /// <summary>
    /// Replaces the horizon with the airings between the watermark and an hour
    /// out. This runs the read path, estimates included, so it is deliberately
    /// not something the loop does every minute.
    /// </summary>
    /// <param name="minute">The minute being processed, in UTC.</param>
    internal void RebuildHorizon(DateTime minute)
    {
        // From the watermark rather than from now, so a rebuild between two
        // ticks cannot drop an airing that has come due but not yet fired.
        var from = _watermark > DateTime.MinValue ? _watermark : minute;
        var to = minute + HorizonLength;
        var airings = _service.GetAiringsInRange(from, to, new()
        {
            IncludeEstimates = true,
            // The delay gap a calendar draws is not an airing happening, and an
            // airing with no current slot never airs at all.
            IncludeDelayedOriginalSlots = false,
        });

        _horizon.Clear();
        foreach (var airing in airings)
        {
            if (airing.AiredAt is not { } airedAt)
                continue;

            airedAt = airedAt.ToUtc();
            if (airedAt <= from || airedAt > to)
                continue;

            _horizon[airing.ID] = new HorizonEntry(airing, airedAt);
        }

        _horizonBuiltAt = minute;
        _logger.LogTrace("Rebuilt the episode airing horizon: {Count} airing(s) between {From:o} and {To:o}.", _horizon.Count, from, to);
    }

    private void OnScheduleUpdated(object? sender, AiringScheduleEventArgs e)
        => InvalidateHorizon();

    private void OnAiringsUpdated(object? sender, EpisodeAiringsUpdatedEventArgs e)
        => InvalidateHorizon();

    private void OnProvidersUpdated(object? sender, EventArgs e)
        => InvalidateHorizon();

    /// <summary>
    /// Marks the horizon for a rebuild on the next tick. A schedule or airing
    /// change has to reach the horizon without waiting for the periodic
    /// refresh, or a delay reported mid-horizon would fire at the old time. The
    /// rebuild is deferred to the tick rather than done inline because the
    /// caller here is a provider's write, and it should not pay for the read
    /// path on its own thread.
    /// </summary>
    internal void InvalidateHorizon()
        => Interlocked.Exchange(ref _horizonStale, 1);

    #endregion

    #region Watermark

    /// <summary>
    /// The instant the ticker has raised events through, in UTC.
    /// </summary>
    internal DateTime Watermark => _watermark;

    /// <summary>
    /// Picks the watermark up at boot and moves it to now without replaying
    /// what was missed.
    /// </summary>
    /// <remarks>
    /// The gap is skipped rather than replayed on purpose: most of what a
    /// stopped server missed is estimates, and an hours-old prediction
    /// announced as if it just happened is worse than saying nothing. The row
    /// is still kept, so the skip is deliberate and visible, and so a short gap
    /// could be replayed later if that ever looks worth doing.
    /// </remarks>
    /// <param name="minute">The minute being processed, in UTC.</param>
    internal void SkipForward(DateTime minute)
    {
        _started = true;
        var previous = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.EpisodeAiringNotifications)?.LastUpdate.ToUtc();
        if (previous is { } through && through < minute)
            _logger.LogInformation(
                "Skipping {Gap} of episode airing notifications: the last one was dispatched through {Through:o}, and stale airings are not replayed.",
                minute - through,
                through
            );

        _watermark = minute;
        SaveWatermark(minute);
    }

    private void SaveWatermark(DateTime minute)
    {
        try
        {
            var row = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.EpisodeAiringNotifications)
                ?? new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.EpisodeAiringNotifications, UpdateDetails = string.Empty };
            row.LastUpdate = minute;
            RepoFactory.ScheduledUpdate.Save(row);
            _watermarkPersistedAt = minute;
        }
        catch (Exception ex)
        {
            // A watermark that cannot be written costs the next boot its gap
            // log line, and nothing else, so it must not take the tick with it.
            _logger.LogError(ex, "Unable to persist the episode airing notification watermark.");
        }
    }

    #endregion

    private static DateTime TruncateToMinute(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Utc);

    /// <summary>
    /// One airing waiting for its slot, with the slot read once as UTC so every
    /// comparison in the tick is UTC against UTC.
    /// </summary>
    /// <param name="Airing">The airing.</param>
    /// <param name="AiredAt">Its slot, in UTC.</param>
    private sealed record HorizonEntry(IEpisodeAiring Airing, DateTime AiredAt);
}
