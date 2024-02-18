﻿namespace Shoko.Server.Providers.AniDB.Interfaces;

public interface IRequest<out T, T1> where T : IResponse<T1> where T1 : class
{
    T Send();
}

public interface IRequest
{
    object Send();
}
