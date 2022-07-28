using System;
using Shoko.Server.Providers.AniDB.Http;

namespace Shoko.Server.Providers.AniDB.Interfaces
{
    public interface IHttpConnectionHandler : IConnectionHandler
    {
        event EventHandler<AniDBStateUpdate> AniDBStateUpdate;
        IServiceProvider ServiceProvider { get; }
        HttpBaseResponse<string> GetHttp(string url);
    }
}
