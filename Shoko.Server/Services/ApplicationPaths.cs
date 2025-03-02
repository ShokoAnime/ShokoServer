
using System.IO;
using System.Reflection;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public class ApplicationPaths : IApplicationPaths
{
    private static ApplicationPaths? _instance = null;

    public static ApplicationPaths Instance
        => _instance ??= new();

    private string? _executableDirectoryPath = null;

    /// <inheritdoc/>
    public string ExecutableDirectoryPath
        => _executableDirectoryPath ??= Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    /// <inheritdoc/>
    public string WebPath => Path.Combine(ProgramDataPath, Utils.SettingsProvider.GetSettings().Web.WebUIPath);

    /// <inheritdoc/>
    public string ProgramDataPath => Utils.ApplicationPath;

    /// <inheritdoc/>
    public string ImageDirectoryPath => ImageUtils.GetBaseImagesPath();

    /// <inheritdoc/>
    public string PluginsPath => Path.Combine(ProgramDataPath, "plugins");

    /// <inheritdoc/>
    public string PluginConfigurationsPath => Path.Combine(Utils.ApplicationPath, "plugins", "configuration");

    /// <inheritdoc/>
    public string LogDirectoryPath => Path.Combine(ProgramDataPath, "logs");
}
