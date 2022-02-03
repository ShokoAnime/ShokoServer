using System;

namespace Shoko.Server.Providers.AniDB.Interfaces
{
    public interface IAniDBSocketHandler : IDisposable
    {
        bool IsLocked { get; }
        byte[] Send(byte[] payload);
        bool TryConnection();
    }
}
