
using System.IO;
using System.Reflection;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public class ApplicationPaths : IApplicationPaths
{
    public static ApplicationPaths Instance { get; set; } = new();

    /// <inheritdoc/>
    public string ExecutableDirectoryPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    /// <inheritdoc/>
    public string WebPath => Path.Combine(ProgramDataPath, Utils.SettingsProvider.GetSettings().Web.WebUIPath);

    /// <inheritdoc/>
    public string ProgramDataPath => Utils.ApplicationPath;

    /// <inheritdoc/>
    public string ImageDirectoryPath => ImageUtils.GetBaseImagesPath();

    /// <inheritdoc/>
    public string PluginsPath => Path.Combine(ProgramDataPath, "plugins");

    /// <inheritdoc/>
    public string LogDirectoryPath => Path.Combine(ProgramDataPath, "logs");
}
