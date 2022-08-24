using System.Net;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB.HTTP
{
    public class HttpResponse<T> : IResponse<T> where T : class
    {
        public HttpStatusCode Code { get; set; }
        
        public T Response { get; set; }
    }
}
