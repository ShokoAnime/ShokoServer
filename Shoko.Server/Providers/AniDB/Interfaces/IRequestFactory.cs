using System;

namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IRequestFactory
{
    T Create<T>(Action<T>? configure = null) where T : class, IRequest;
}
