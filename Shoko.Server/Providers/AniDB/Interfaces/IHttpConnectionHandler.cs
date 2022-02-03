using Shoko.Server.Providers.AniDB.Http;

namespace Shoko.Server.Providers.AniDB.Interfaces
{
    public interface IHttpConnectionHandler : IConnectionHandler
    {
        HttpBaseResponse<string> GetHttp(string url);
    }
}
