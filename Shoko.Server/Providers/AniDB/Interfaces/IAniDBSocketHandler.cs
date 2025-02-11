using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IAniDBSocketHandler : IDisposable, IAsyncDisposable
{
    bool IsConnected { get; }
    Task<byte[]> Send(byte[] payload, CancellationToken token = new());
    Task<bool> TryConnection();
}
