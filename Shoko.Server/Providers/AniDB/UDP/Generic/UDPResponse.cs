using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB.UDP.Generic;

public class UDPResponse<T> : IResponse<T> where T : class
{
    public UDPReturnCode Code { get; set; }

    public T Response { get; set; }
}
