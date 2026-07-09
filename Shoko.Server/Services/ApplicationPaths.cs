using System;
using System.IO;
using System.Reflection;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

public class ApplicationPaths : IApplicationPaths
{
    private static ApplicationPaths? _instance = null;

    public static IApplicationPaths Instance
        => _instance ??= new();

    private string? _applicationPath = null;

    /// <inheritdoc/>
    public string ApplicationPath
        => _applicationPath ??= Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    private string? _webPath = null;

    /// <inheritdoc/>
    public string WebPath
        => _webPath ??= Path.Combine(DataPath, ISettingsProvider.Instance.GetSettings().Web.WebUIPath);

    private static string? _dataPath = null;

    /// <inheritdoc/>
    public string DataPath => StaticDataPath;

    public static string StaticDataPath
    {
        get
        {
            if (_dataPath != null)
                return _dataPath;

            var defaultHome = GetDefaultHome();
            var shokoHome = Environment.GetEnvironmentVariable("SHOKO_HOME");
            if (!string.IsNullOrWhiteSpace(shokoHome))
            {
                if (!Path.IsPathFullyQualified(shokoHome))
                    shokoHome = Path.Combine(defaultHome, shokoHome);
                return _dataPath = Path.GetFullPath(shokoHome);
            }

            return _dataPath = defaultHome;
        }
    }

    private static string GetDefaultHome()
    {
        var instanceName = Assembly.GetEntryAssembly()?.GetName().Name ?? "ShokoServer";
        if (!PlatformUtility.IsWindows)
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".shoko",
                instanceName);

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            instanceName);
    }

    private static string? GetHomeFromCommandLineArguments(string[] args)
    {
        const int NotFound = -1;
        var idx = Array.FindIndex(args, x => string.Equals(x, "--home", StringComparison.InvariantCultureIgnoreCase));
        if (idx is NotFound)
            return null;
        if (idx >= args.Length - 1)
            return null;
        var value = args[idx + 1];
        if (value.StartsWith("--", StringComparison.InvariantCultureIgnoreCase))
            return null;
        return value;
    }

    public static void SetHome(string[] args)
    {
        var home = GetHomeFromCommandLineArguments(args);
        if (string.IsNullOrWhiteSpace(home))
            return;

        if (!Path.IsPathFullyQualified(home))
            home = Path.Combine(GetDefaultHome(), home);

        _dataPath = home;
    }

    private string? _imagesPath = null;

    /// <inheritdoc/>
    public string ImagesPath
        => _imagesPath ??= ISettingsProvider.Instance.GetSettings().ImagesPath is { Length: > 0 } imagePath
            ? Path.Combine(DataPath, imagePath)
            : DefaultImagePath;

    public static string DefaultImagePath => Path.Combine(StaticDataPath, "images");

    /// <inheritdoc/>
    public string PluginsPath
        => Path.Combine(DataPath, "plugins");

    public string ThemesPath
        => Path.Combine(DataPath, "themes");

    /// <inheritdoc/>
    public string ConfigurationsPath
        => Path.Combine(DataPath, "configuration");

    /// <inheritdoc/>
    public string LogsPath
        => Path.Combine(DataPath, "logs");
}
