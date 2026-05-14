using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Utilities;

public static partial class Utils
{
    public static IServiceProvider ServiceContainer { get; set; }

    public static ISettingsProvider SettingsProvider { get; set; }

    public static string GetDistinctPath(string fullPath)
    {
        var parent = Path.GetDirectoryName(fullPath);
        return string.IsNullOrEmpty(parent) ? fullPath : Path.Combine(Path.GetFileName(parent), Path.GetFileName(fullPath));
    }
}
