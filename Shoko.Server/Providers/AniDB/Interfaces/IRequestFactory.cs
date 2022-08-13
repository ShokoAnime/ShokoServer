using System;

namespace Shoko.Server.Providers.AniDB.Interfaces
{
    public interface IRequestFactory
    {
        T1 Create<T1, T2>(Action<T1> ctor) where T1 : IRequest<T2>, new() where T2 : IResponse;
    }
}
