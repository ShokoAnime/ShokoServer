using System;
using Microsoft.Extensions.Options;

namespace Shoko.Plugin.Abstractions.Configuration
{
    public interface IWritableOptions<out T> : IOptions<T> where T : class, IDefaultedConfig, new()
    {
        void Update(Action<T> applyChanges);
    }
}
