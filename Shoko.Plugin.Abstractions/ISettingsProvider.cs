using System;

namespace Shoko.Plugin.Abstractions
{
    [Obsolete("Use new method, ask in discord if you need help", true)]
    public interface ISettingsProvider
    {
        void SaveSettings(IPluginSettings settings);
    }
}