using System;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Shoko.Core.Addon;

namespace Shoko.Plugin.AniDB
{
    [Plugin("AniDB")]
    public class AniDBPlugin : IPlugin
    {
        AniDBConfig config = new AniDBConfig();

        /// <inheritdoc/>
        public object Configuration => config;

        /// <inheritdoc/>
        public void LoadConfiguration(JToken config)
        {
            this.config = config.ToObject<AniDBConfig>();
            Debugger.Break();
        }
    }

    public class AniDBConfig
    {
        public int Foo { get; set; }
    }
}
