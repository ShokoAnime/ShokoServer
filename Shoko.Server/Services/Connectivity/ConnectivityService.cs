using System;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Commands;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Services.Connectivity;

public class ConnectivityService : IConnectivityService
{
    private readonly IUDPConnectionHandler AnidbUdpHandler;

    private readonly IHttpConnectionHandler AnidbHttpHandler;

    private readonly CommandProcessor GeneralQueue;

    private readonly CommandProcessor ImagesQueue;

    private NetworkAvailability _networkAvailability { get; set; } = NetworkAvailability.NoInterfaces;

    /// <inheritdoc/>
    public event EventHandler<NetworkAvailabilityChangedEventArgs> NetworkAvailabilityChanged;

    /// <inheritdoc/>
    public NetworkAvailability NetworkAvailability
    {
        get => _networkAvailability;
        set
        {
            var hasChanged = _networkAvailability != value;
            _networkAvailability = value;
            if (hasChanged)
                Task.Run(() => NetworkAvailabilityChanged?.Invoke(null, new(value))).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public bool IsAniDBUdpReachable =>
        AnidbUdpHandler.IsNetworkAvailable;

    /// <inheritdoc/>
    public bool IsAniDBHttpBanned =>
        AnidbHttpHandler.IsBanned;

    /// <inheritdoc/>
    public bool IsAniDBUdpBanned =>
        AnidbUdpHandler.IsBanned;

    public ConnectivityService(IUDPConnectionHandler udpHandler, IHttpConnectionHandler httpHandler, CommandProcessorGeneral generalQueue, CommandProcessorImages imagesQueue)
    {
        AnidbUdpHandler = udpHandler;
        AnidbHttpHandler = httpHandler;
        GeneralQueue = generalQueue;
        ImagesQueue = imagesQueue;
        NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    ~ConnectivityService()
    {
        NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }

    // Notify the queues that they can start again.
    private void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityChangedEventArgs eventArgs)
    {
        if (!eventArgs.NetworkAvailability.HasInternet())
            return;

        if (!GeneralQueue.Paused && GeneralQueue.QueueCount > 0)
                GeneralQueue.NotifyOfNewCommand();

        if (!ImagesQueue.Paused && ImagesQueue.QueueCount > 0)
            ImagesQueue.NotifyOfNewCommand();
    }
}
