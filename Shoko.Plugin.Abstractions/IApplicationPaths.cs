
namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Interface IApplicationPaths.
/// </summary>
public interface IApplicationPaths
{
    /// <summary>
    /// Gets the path to the executable parent directory.
    /// </summary>
    /// <value>The program data path.</value>
    string ExecutableDirectoryPath { get; }

    /// <summary>
    /// Gets the path to the web UI resources folder.
    /// </summary>
    /// <value>The web path.</value>
    string WebPath { get; }

    /// <summary>
    /// Gets the folder path to the data directory.
    /// </summary>
    /// <value>The data directory.</value>
    string ProgramDataPath { get; }

    /// <summary>
    /// Gets the image directory path.
    /// </summary>
    /// <value>The image directory path.</value>
    string ImageDirectoryPath { get; }

    /// <summary>
    /// Gets the path to the plugin directory.
    /// </summary>
    /// <value>The plugins path.</value>
    string PluginsPath { get; }

    /// <summary>
    /// Gets the path to the plugin configuration directory.
    /// </summary>
    /// <value>The plugin configuration path.</value>
    string PluginConfigurationsPath { get; }

    /// <summary>
    /// Gets the path to the log directory.
    /// </summary>
    /// <value>The log directory path.</value>
    string LogDirectoryPath { get; }
}
