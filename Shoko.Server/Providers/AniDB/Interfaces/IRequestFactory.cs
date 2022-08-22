using System;

namespace Shoko.Server.Providers.AniDB.Interfaces
{
    public interface IRequestFactory
    {
        T Create<T>(Action<T> ctor = null) where T : class, IRequest;
        
        T Create<T, T1>(Action<T> ctor = null) where T : IRequest<IResponse<T1>, T1> where T1 : class;
    }
}
