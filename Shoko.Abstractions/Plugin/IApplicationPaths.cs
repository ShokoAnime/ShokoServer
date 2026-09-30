
namespace Shoko.Abstractions.Plugin;

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
    /// Gets the path to the plugin directory. Only installed plugins go here;
    /// a plugin keeps nothing it writes at runtime under it.
    /// </summary>
    /// <value>The plugins path.</value>
    string PluginsPath { get; }

    /// <summary>
    /// Gets the path to the directory new databases live in, such as the ones
    /// plugins keep. Each plugin gets a folder of its own under it, named by
    /// its ID. The core's own database stays where the database settings put
    /// it. Everything here is included in the server's backups.
    /// </summary>
    /// <value>The database directory path, <c>data</c> under the data directory.</value>
    string DatabasePath { get; }

    /// <summary>
    /// Gets the path to the cache directory, for files that can be fetched or
    /// built again and can be deleted at any time. Each plugin gets a folder of
    /// its own under it, named by its ID. Backups leave it out. Transcodes have
    /// their own directory, <see cref="StreamCachePath"/>.
    /// </summary>
    /// <value>The cache directory path, <c>cache</c> under the data directory.</value>
    string CachePath { get; }

    /// <summary>
    /// Gets the path to the video stream transcode/rendition cache directory.
    /// </summary>
    /// <value>The stream cache directory path.</value>
    string StreamCachePath { get; }

    /// <summary>
    /// Gets the path to the themes directory.
    /// </summary>
    string ThemesPath { get; }

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
