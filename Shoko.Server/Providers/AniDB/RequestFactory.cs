using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Providers.AniDB.Interfaces;

namespace Shoko.Server.Providers.AniDB;

public class RequestFactory : IRequestFactory
{
    private readonly IServiceProvider _provider;

    public RequestFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public T Create<T>(Action<T>? configure = null) where T : class, IRequest
    {
        var obj = ActivatorUtilities.CreateInstance<T>(_provider);
        configure?.Invoke(obj);
        return obj;
    }
}
