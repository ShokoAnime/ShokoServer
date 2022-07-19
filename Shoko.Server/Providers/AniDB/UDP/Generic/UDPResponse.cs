namespace Shoko.Server.Providers.AniDB.UDP.Generic
{
    public class UDPResponse<T> where T : class
    {
        public UDPReturnCode Code { get; set; }
        
        public T Response { get; set; }
    }
}
