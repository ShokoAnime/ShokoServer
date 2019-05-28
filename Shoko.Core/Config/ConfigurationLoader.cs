using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Shoko.Core.Extensions;
using Shoko.Core.Addon;

namespace Shoko.Core.Config
{
    internal static class ConfigurationLoader
    {
        private const string CORE_CONFIG_PROP = "core";
        public static string Json = "{ \"core\": {}, \"AniDB\": {\"Foo\": 1} }";
        public static CoreConfig CoreConfig { get; set; } = new CoreConfig(); //at least have defaults.
        private static JObject _jsonConfig;

        public static void LoadConfig()
        {
            _jsonConfig = JObject.Parse(Json);
            if (_jsonConfig.ContainsKey(CORE_CONFIG_PROP))
            {
                CoreConfig = _jsonConfig[CORE_CONFIG_PROP].ToObject<CoreConfig>();
            }
        }

        public static void LoadPluginConfig()
        {
            if (!AddonRegistry.Initalized) throw new InvalidOperationException("Plugin loader must run before you can call this.");
            foreach ((string key, JToken token) in _jsonConfig)
            {
                if (!AddonRegistry.Plugins.TryGetValue(key, out var plugin)) continue;
                plugin.LoadConfiguration(token.DeepClone());
            }
        }
    }
}
