
namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Exposes information about the application directories to plugins.
/// </summary>
public interface IApplicationPaths
{
    /// <summary>
    /// Gets the path to the executable parent directory.
    /// </summary>
    /// <value>The program data path.</value>
    string ApplicationPath { get; }

    /// <summary>
    /// Gets the path to the Web UI resources directory.
    /// </summary>
    /// <value>The Web UI directory path.</value>
    string WebPath { get; }

    /// <summary>
    /// Gets the path to the data directory.
    /// </summary>
    /// <value>The data directory path.</value>
    string DataPath { get; }

    /// <summary>
    /// Gets the path to the image directory path.
    /// </summary>
    /// <value>The image directory path.</value>
    string ImagesPath { get; }

    /// <summary>
    /// Gets the path to the plugin directory.
    /// </summary>
    /// <value>The plugins path.</value>
    string PluginsPath { get; }

    /// <summary>
    /// Gets the path to the configuration directory.
    /// </summary>
    /// <value>The configuration directory path.</value>
    string ConfigurationsPath { get; }

    /// <summary>
    /// Gets the path to the log directory.
    /// </summary>
    /// <value>The log directory path.</value>
    string LogsPath { get; }
}
