namespace Shoko.Server.Providers.AniDB.Interfaces;

/// <summary>
/// Creates the UDP socket the AniDB connection handler talks through.
/// </summary>
/// <remarks>
/// Exists so the connection handler can be exercised without opening a socket: the protocol
/// handling around the socket — session state, ban detection, response codes — is where the logic
/// lives, and none of it should need the network to test.
/// </remarks>
public interface IAniDBSocketHandlerFactory
{
    /// <summary>
    /// Creates a socket handler bound to the given server and local port.
    /// </summary>
    IAniDBSocketHandler Create(string host, ushort serverPort, ushort clientPort);
}
