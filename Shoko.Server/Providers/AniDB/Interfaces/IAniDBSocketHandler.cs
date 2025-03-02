using System;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IAniDBSocketHandler : IDisposable, IAsyncDisposable
{
    bool IsConnected { get; }
    byte[] Send(byte[] payload);
    bool TryConnection();
}
