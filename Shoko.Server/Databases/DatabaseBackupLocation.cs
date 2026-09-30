using System.IO;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Settings;

namespace Shoko.Server.Databases;

/// <summary>
/// Where database backups go.
/// </summary>
internal static class DatabaseBackupLocation
{
    /// <summary>
    /// The backup folder from the database settings, under the data folder,
    /// or the data folder itself when the setting is empty.
    /// </summary>
    /// <param name="applicationPaths">The server's directories.</param>
    /// <returns>The folder.</returns>
    internal static string GetDirectory(IApplicationPaths applicationPaths)
    {
        var directory = ISettingsProvider.Instance.GetSettings().Database.DatabaseBackupDirectory;
        return string.IsNullOrWhiteSpace(directory)
            ? applicationPaths.DataPath
            : Path.Combine(applicationPaths.DataPath, directory);
    }
}
