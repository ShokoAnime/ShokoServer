namespace Shoko.Server.Providers.AniDB.UDP.Generic.Responses
{
    public class UDPBaseResponse<T> where T : class
    {
        public AniDBUDPReturnCode Code { get; set; }
        
        public T Response { get; set; }
    }
}
