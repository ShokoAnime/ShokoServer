using System;
using System.Threading.Tasks;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IAniDBSocketHandler : IDisposable, IAsyncDisposable
{
    bool IsLocked { get; }
    bool IsConnected { get; }
    Task<byte[]> Send(byte[] payload);
    Task<bool> TryConnection();
}
