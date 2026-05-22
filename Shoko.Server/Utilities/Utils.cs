using System;
using System.IO;
using Shoko.Server.Settings;

namespace Shoko.Server.Utilities;

public static partial class Utils
{
    public static IServiceProvider ServiceContainer { get; set; }

    public static ISettingsProvider SettingsProvider { get; set; }
}
