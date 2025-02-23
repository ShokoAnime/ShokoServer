using Shoko.Server.Providers.AniDB.HTTP;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IHttpConnectionHandler : IConnectionHandler
{
    Task<HttpResponse<string>> GetHttp(string url);
}
