using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB.Suspensions;

/// <summary>
///   Reports the AniDB connections' state as suspensions: a ban on either
///   API, and the UDP API's overload backoff, invalid session and refused
///   credentials. Waiting on a response or a dead connection is not one.
/// </summary>
/// <param name="udpHandler">The UDP connection.</param>
/// <param name="httpHandler">The HTTP connection.</param>
/// <param name="udpReporter">Reports the UDP API's suspensions.</param>
/// <param name="httpReporter">Reports the HTTP API's suspensions.</param>
/// <param name="logger">Where a failed report is logged.</param>
public sealed class AnidbSuspensionMonitor(
    IUDPConnectionHandler udpHandler,
    IHttpConnectionHandler httpHandler,
    ISuspensionReporter<AnidbUdpSuspensionProvider> udpReporter,
    ISuspensionReporter<AnidbHttpSuspensionProvider> httpReporter,
    ILogger<AnidbSuspensionMonitor> logger
) : IHostedService
{
    #region Lifecycle

    /// <summary>
    ///   Starts listening, after the providers are registered, and reports
    ///   what is on already.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        udpHandler.AniDBStateUpdate += OnUdpStateUpdate;
        httpHandler.AniDBStateUpdate += OnHttpStateUpdate;

        if (udpHandler.IsBanned)
            Report(udpReporter, SuspensionKind.Banned, true, resumesAt: udpHandler.BanState.BanExpiresUtc, isLiftable: true);
        if (udpHandler.IsInvalidSession)
            Report(udpReporter, SuspensionKind.SessionInvalid, true);
        if (udpHandler.IsLoginFailed)
            Report(udpReporter, SuspensionKind.AuthenticationFailed, true);
        if (httpHandler.IsBanned)
            Report(httpReporter, SuspensionKind.Banned, true, resumesAt: httpHandler.BanState.BanExpiresUtc, isLiftable: true);

        return Task.CompletedTask;
    }

    /// <summary>
    ///   Stops listening.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        udpHandler.AniDBStateUpdate -= OnUdpStateUpdate;
        httpHandler.AniDBStateUpdate -= OnHttpStateUpdate;
        return Task.CompletedTask;
    }

    #endregion

    #region Updates

    private void OnUdpStateUpdate(object? sender, AniDBStateUpdate update)
    {
        switch (update.UpdateType)
        {
            case UpdateType.UDPBan:
                Report(udpReporter, SuspensionKind.Banned, update.Value, resumesAt: udpHandler.BanState.BanExpiresUtc, isLiftable: true);
                break;
            case UpdateType.OverloadBackoff:
                Report(udpReporter, SuspensionKind.Overloaded, update.Value, update.Message, DateTime.UtcNow.AddSeconds(update.PauseTimeSecs));
                break;
            case UpdateType.InvalidSession:
                Report(udpReporter, SuspensionKind.SessionInvalid, update.Value);
                break;
            case UpdateType.LoginFailed:
                Report(udpReporter, SuspensionKind.AuthenticationFailed, update.Value);
                break;
        }
    }

    private void OnHttpStateUpdate(object? sender, AniDBStateUpdate update)
    {
        if (update.UpdateType is UpdateType.HTTPBan)
            Report(httpReporter, SuspensionKind.Banned, update.Value, resumesAt: httpHandler.BanState.BanExpiresUtc, isLiftable: true);
    }

    private void Report<TProvider>(
        ISuspensionReporter<TProvider> reporter,
        SuspensionKind kind,
        bool on,
        string? reason = null,
        DateTime? resumesAt = null,
        bool isLiftable = false
    ) where TProvider : ISuspensionProvider
    {
        try
        {
            if (on)
                reporter.Suspend(kind, reason, resumesAt, isLiftable);
            else
                reporter.Resume(kind);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Could not report the {Kind} suspension of {Provider}.", kind, typeof(TProvider).Name);
        }
    }

    #endregion
}
