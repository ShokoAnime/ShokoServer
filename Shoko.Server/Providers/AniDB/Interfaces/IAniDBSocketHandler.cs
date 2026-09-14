using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IAniDBSocketHandler : IDisposable, IAsyncDisposable
{
    bool IsConnected { get; }

    Task<byte[]> SendAsync(byte[] payload, CancellationToken cancellationToken = default);

    Task<bool> TryConnectionAsync(CancellationToken cancellationToken = default);
}
