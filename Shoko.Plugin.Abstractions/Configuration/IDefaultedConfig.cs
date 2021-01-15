using System;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Plugin.Abstractions.Configuration
{
    public interface IDefaultedConfig
    {
        /// <summary>
        /// Set the defaults to the object itself.
        /// This is to work around 
        /// </summary>
        void SetDefaults();
    }
}
