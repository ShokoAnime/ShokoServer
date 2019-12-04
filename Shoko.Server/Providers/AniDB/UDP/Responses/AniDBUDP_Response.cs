namespace Shoko.Server.Providers.AniDB.UDP.Responses
{
    public class AniDBUDP_Response<T> where T : class
    {
        public AniDBUDPReturnCode Code { get; set; }
        
        public T Response { get; set; }
    }
}
