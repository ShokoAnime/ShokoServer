using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Shoko.Plugin.Abstractions.Configuration
{
    public static class ConfigurationExtensions
    {
        public static T GetDefaulted<T>(this IConfiguration configuration, string section) where T : IDefaultedConfig, new()
        {
            var value = configuration.GetSection(section).Get<T>() ?? new T();
            value.SetDefaults();

            return value;
        }
    }
}
