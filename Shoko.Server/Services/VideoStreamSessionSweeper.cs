using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

/// <summary>
/// Evicts idle stream sessions once a minute, on its own clock.
/// </summary>
/// <remarks>
/// This is an <see cref="IHostedService"/> rather than a queue job or a
/// scheduled action because it must keep running while the queue is paused or
/// busy: an idle session holds its rendition, and with it a transcoder and its
/// cache, until it is swept.
/// </remarks>
/// <param name="logger">The logger.</param>
/// <param name="sessionManager">The stream sessions.</param>
/// <param name="configurationProvider">The stream settings, read on every sweep.</param>
public sealed class VideoStreamSessionSweeper(
    ILogger<VideoStreamSessionSweeper> logger,
    VideoStreamSessionManager sessionManager,
    ConfigurationProvider<VideoStreamPipelineSettings> configurationProvider
) : BackgroundService
{
    #region Constants

    /// <summary>
    /// How often idle sessions are swept.
    /// </summary>
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    #endregion

    #region Sweeping

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            Sweep();
        }
        while (await WaitForNextTick(timer, stoppingToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Evicts the sessions idle for longer than the settings allow. A failure
    /// is logged, never thrown, so one bad sweep does not stop the next.
    /// </summary>
    internal void Sweep()
    {
        try
        {
            var idleTimeout = TimeSpan.FromMinutes(configurationProvider.Load().SessionIdleTimeoutMinutes);
            sessionManager.EvictExpiredSessions(idleTimeout);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sweep the idle stream sessions.");
        }
    }

    /// <summary>
    /// Waits for the next tick.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="stoppingToken">Cancelled when the host stops.</param>
    /// <returns><c>false</c> once the host is stopping.</returns>
    private static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    #endregion
}
