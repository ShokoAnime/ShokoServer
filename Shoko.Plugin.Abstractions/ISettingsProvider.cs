using System;

namespace Shoko.Plugin.Abstractions
{
    public interface ISettingsProvider<T> where T : class
    {
        TResult Get<TResult>(Func<T, TResult> func);
        void Update(Action<T> func);
    }
}