using System;

namespace Shoko.Plugin.Abstractions
{
    public interface ISettingsProvider<T> where T : class
    {
        T Get(Func<T, T> func);
        void Update(Action<T> func);
    }
}