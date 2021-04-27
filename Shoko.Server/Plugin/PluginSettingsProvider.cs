using System;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Plugin
{
    public class PluginSettingsProvider<T> : ISettingsProvider<T> where T : class
    {
        private T Settings { get; set; }
        
        public PluginSettingsProvider()
        {
            // TODO read settings
            Settings = JsonConvert.DeserializeObject<T>("");
        }

        public TResult Get<TResult>(Func<T, TResult> func)
        {
            return func.Invoke(Settings);
        }
        
        public void Update(Action<T> func)
        {
            // update via lambda
            func.Invoke(Settings);
            // TODO save settings
        }
    }
}
