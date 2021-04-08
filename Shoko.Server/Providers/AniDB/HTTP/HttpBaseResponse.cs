using System.Net;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class HttpBaseResponse<T> where T : class
    {
        public HttpStatusCode Code { get; set; }
        
        public T Response { get; set; }
    }
}
