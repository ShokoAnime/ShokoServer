using System.Threading;
using System.Threading.Tasks;
using Shoko.Server.Providers.AniDB.HTTP;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IHttpConnectionHandler : IConnectionHandler
{
    /// <summary>
    /// Sends a GET request to the AniDB HTTP API.
    /// </summary>
    /// <param name="url">The URL or path to request.</param>
    /// <param name="force">Bypass the ban check. A ban response still registers the ban but does not extend an existing one.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="AniDBBannedException">Thrown when banned, or the server responds 403 / indicates a ban in the response body.</exception>
    Task<HttpResponse<string>> GetHttp(string url, bool force = false, CancellationToken cancellationToken = default);
}
