using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB
{
    public class RequestFactory : IRequestFactory
    {
        private readonly ServiceProvider _provider;

        public RequestFactory(ServiceProvider provider)
        {
            _provider = provider;
        }

        public T1 Create<T1, T2>(Action<T1> ctor = null) where T1 : IRequest<T2>, new() where T2 : IResponse
        {
            var obj = ActivatorUtilities.CreateInstance<T1>(_provider);
            ctor?.Invoke(obj);
            return obj;
        }
    }
}
