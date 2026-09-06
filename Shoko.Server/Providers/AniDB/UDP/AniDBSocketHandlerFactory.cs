using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB.UDP;

/// <inheritdoc cref="IAniDBSocketHandlerFactory"/>
public class AniDBSocketHandlerFactory(ILoggerFactory loggerFactory) : IAniDBSocketHandlerFactory
{
    /// <inheritdoc/>
    public IAniDBSocketHandler Create(string host, ushort serverPort, ushort clientPort)
        => new AniDBSocketHandler(loggerFactory, host, serverPort, clientPort);
}
