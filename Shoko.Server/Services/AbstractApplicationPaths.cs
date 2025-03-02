
using System.IO;
using System.Reflection;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public class AbstractApplicationPaths : IApplicationPaths
{
    private static AbstractApplicationPaths? _instance = null;

    public static AbstractApplicationPaths Instance
        => _instance ??= new();

    private string? _executableDirectoryPath = null;

    /// <inheritdoc/>
    public string ExecutableDirectoryPath
        => _executableDirectoryPath ??= Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    /// <inheritdoc/>
    public string WebPath => Path.Combine(ProgramDataPath, "webui");

    /// <inheritdoc/>
    public string ProgramDataPath => Utils.ApplicationPath;

    /// <inheritdoc/>
    public string ImageDirectoryPath => ImageUtils.GetBaseImagesPath();

    /// <inheritdoc/>
    public string PluginsPath => Path.Combine(Utils.ApplicationPath, "plugins");

    /// <inheritdoc/>
    public string PluginConfigurationsPath => Path.Combine(Utils.ApplicationPath, "plugins", "configuration");

    /// <inheritdoc/>
    public string LogDirectoryPath => Path.Combine(Utils.ApplicationPath, "logs");
}
