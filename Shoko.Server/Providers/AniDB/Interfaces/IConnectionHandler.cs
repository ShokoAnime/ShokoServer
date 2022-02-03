using System;

namespace Shoko.Server.Providers.AniDB.Interfaces
{
    public interface IConnectionHandler
    {
        string Type { get; }
        int BanTimerResetLength { get; }
        DateTime? BanTime { get; set; }
        event EventHandler<AniDBStateUpdate> AniDBStateUpdate;
        AniDBStateUpdate State { get; set; }
        bool IsBanned { get; set; }
    }
}
