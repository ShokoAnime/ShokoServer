
using System.IO;
using System.Reflection;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public class AbstractApplicationPaths : IApplicationPaths
{
    public static AbstractApplicationPaths Instance => new();

    /// <inheritdoc/>
    public string ExecutableDirectoryPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    /// <inheritdoc/>
    public string WebPath => Path.Combine(ProgramDataPath, "webui");

    /// <inheritdoc/>
    public string ProgramDataPath => Utils.ApplicationPath;

    /// <inheritdoc/>
    public string ImageDirectoryPath => ImageUtils.GetBaseImagesPath();

    /// <inheritdoc/>
    public string PluginsPath => Path.Combine(ProgramDataPath, "plugins");

    /// <inheritdoc/>
    public string LogDirectoryPath => Path.Combine(ProgramDataPath, "logs");
}
