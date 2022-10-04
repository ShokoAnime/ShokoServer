using System;
using Shoko.Server.Providers.AniDB.HTTP;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IHttpConnectionHandler : IConnectionHandler
{
    event EventHandler<AniDBStateUpdate> AniDBStateUpdate;
    IServiceProvider ServiceProvider { get; }
    HttpResponse<string> GetHttp(string url);
}
