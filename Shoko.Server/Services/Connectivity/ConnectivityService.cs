using System;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Services.Connectivity;

public class ConnectivityService : IConnectivityService
{
    private readonly IUDPConnectionHandler AnidbUdpHandler;

    private readonly IHttpConnectionHandler AnidbHttpHandler;

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
                Task.Run(() => NetworkAvailabilityChanged?.Invoke(null, new(value)));
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

    public ConnectivityService(IUDPConnectionHandler udpHandler, IHttpConnectionHandler httpHandler)
    {
        AnidbUdpHandler = udpHandler;
        AnidbHttpHandler = httpHandler;
    }
}
