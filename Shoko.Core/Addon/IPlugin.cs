using System;
using System.Collections.Generic;
using System.Text;
using Autofac;
using Newtonsoft.Json.Linq;

namespace Shoko.Core.Addon
{
    /// <summary>
    /// The main interface for a plugin for shoko, ALL plugins must implement this.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Load the config from the given <see cref="JToken"/>
        /// This can simply be parsed into a POCO class by using <see cref="JToken.ToObject{T}()"/>
        /// </summary>
        /// <param name="config">The <see cref="JToken"/> representing the configuration.</param>
        void LoadConfiguration(JToken config);

        /// <summary>
        /// Get the configuration for when it is being written to disk.
        /// </summary>
        /// <returns></returns>
        object Configuration { get; }
    }
}
