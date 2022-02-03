using System;

namespace Shoko.Plugin.Abstractions
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PluginAttribute : Attribute
    {
        public string PluginId { get; set; }

        public PluginAttribute(string pluginId)
        {
            PluginId = pluginId;
        }
    }
}