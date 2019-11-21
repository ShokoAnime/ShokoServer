using AniDBAPI;

namespace Shoko.Server.Providers.AniDB.MyList
{
    public class AniDBUDP_Response<T> where T : class
    {
        public AniDBUDPResponseCode Code { get; set; }
        
        public T Response { get; set; }
    }
}