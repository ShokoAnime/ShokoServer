
using System.IO;
using System.Reflection;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Utilities;

#nullable enable
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
        => _webPath ??= Path.Combine(DataPath, Utils.SettingsProvider.GetSettings().Web.WebUIPath);

    private string? _dataPath = null;

    /// <inheritdoc/>
    public string DataPath => _dataPath ??= Utils.ApplicationPath;

    private string? _imagesPath = null;

    /// <inheritdoc/>
    public string ImagesPath
        => _imagesPath ??= Utils.SettingsProvider.GetSettings().ImagesPath is { Length: > 0 } imagePath
            ? Path.Combine(Utils.ApplicationPath, imagePath)
            : Utils.DefaultImagePath;

    /// <inheritdoc/>
    public string PluginsPath
        => Path.Combine(DataPath, "plugins");

    /// <inheritdoc/>
    public string ConfigurationsPath
        => Path.Combine(DataPath, "configuration");

    /// <inheritdoc/>
    public string LogsPath
        => Path.Combine(DataPath, "logs");
}
