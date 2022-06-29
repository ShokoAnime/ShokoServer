using System;

namespace Shoko.Plugin.Abstractions
{
    public class SettingsSavedEventArgs : EventArgs
    {
        public string Before;
        
        public string After;
    }
}