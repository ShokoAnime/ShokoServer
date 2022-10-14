using System;

namespace Shoko.Server.Settings.DI;

public interface IConfiguration<T> : IDisposable
{
    public T Instance { get; }
}
