using System.Threading.Tasks;
using Shoko.Server.Providers.AniDB.HTTP;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IHttpConnectionHandler : IConnectionHandler
{
    HttpResponse<string> GetHttp(string url);
}
