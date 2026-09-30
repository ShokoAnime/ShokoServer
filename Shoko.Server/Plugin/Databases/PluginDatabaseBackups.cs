using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Plugin;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// Copies of plugin databases: the one taken before migrating, and the ones
/// kept next to a backup of the core's database.
/// </summary>
internal static class PluginDatabaseBackups
{
    /// <summary>
    /// Copies a plugin database through SQLite's backup API, so the copy is
    /// whole even while the file is open in WAL mode, then drops the oldest
    /// copies past <paramref name="keep"/>.
    /// </summary>
    /// <param name="databaseFile">The database file.</param>
    /// <param name="backupDirectory">The folder the copies of this plugin go in.</param>
    /// <param name="name">The database's name.</param>
    /// <param name="now">The time to stamp the copy with.</param>
    /// <param name="keep">How many copies of this database to keep.</param>
    /// <returns>The copy's path.</returns>
    internal static string Backup(string databaseFile, string backupDirectory, string name, DateTime now, int keep)
        => Backup(backupDirectory, name, PluginPathRules.DatabaseExtension, now, keep, backupFile => CopyDatabase(databaseFile, backupFile));

    /// <summary>
    /// Writes a copy of a plugin database, stamped with the time, then drops
    /// the oldest copies with the same extension past <paramref name="keep"/>.
    /// </summary>
    /// <param name="backupDirectory">The folder the copies of this plugin go in.</param>
    /// <param name="name">The database's name.</param>
    /// <param name="extension">The copy's extension, with its dot.</param>
    /// <param name="now">The time to stamp the copy with.</param>
    /// <param name="keep">How many copies of this database to keep.</param>
    /// <param name="write">Writes the copy to the path it is given.</param>
    /// <returns>The copy's path.</returns>
    internal static string Backup(string backupDirectory, string name, string extension, DateTime now, int keep, Action<string> write)
    {
        Directory.CreateDirectory(backupDirectory);
        var backupFile = Path.Join(backupDirectory, $"{name}_{now:yyyyMMddHHmmss}{extension}");
        write(backupFile);

        var pattern = new Regex($"^{Regex.Escape(name)}_\\d{{14}}{Regex.Escape(extension)}$", RegexOptions.CultureInvariant);
        var stale = Directory.EnumerateFiles(backupDirectory)
            .Where(file => pattern.IsMatch(Path.GetFileName(file)))
            .OrderByDescending(file => Path.GetFileName(file), StringComparer.Ordinal)
            .Skip(keep);
        foreach (var file in stale)
            File.Delete(file);

        return backupFile;
    }

    /// <summary>
    /// Copies a SQLite database through its backup API.
    /// </summary>
    /// <param name="source">The database file.</param>
    /// <param name="destination">The copy, replaced if it exists.</param>
    internal static void CopyDatabase(string source, string destination)
    {
        if (File.Exists(destination))
            File.Delete(destination);

        using var from = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        using var to = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
        from.Open();
        to.Open();
        from.BackupDatabase(to);
    }

    /// <summary>
    /// Copies every plugin's database folder next to a backup of the core's
    /// database, so they restore together. Databases are copied through
    /// SQLite's backup API, other files as they are; SQLite's side files and
    /// the caches are left out.
    /// </summary>
    /// <param name="applicationPaths">The server's directories.</param>
    /// <param name="backupName">The core backup's path without its extension.</param>
    /// <param name="logger">The logger, for files that could not be copied.</param>
    /// <returns>The folder the plugin databases went in, or <see langword="null"/> when there were none.</returns>
    internal static string? CopyWithBackup(IApplicationPaths applicationPaths, string backupName, ILogger logger)
    {
        var root = applicationPaths.DatabasePath;
        if (!Directory.Exists(root) || !Directory.EnumerateFileSystemEntries(root).Any())
            return null;

        var destinationRoot = backupName + ".plugin-data";
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith("-wal", StringComparison.Ordinal) || file.EndsWith("-shm", StringComparison.Ordinal) || file.EndsWith("-journal", StringComparison.Ordinal))
                continue;

            var destination = Path.Join(destinationRoot, Path.GetRelativePath(root, file));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (file.EndsWith(PluginPathRules.DatabaseExtension, StringComparison.OrdinalIgnoreCase))
                    CopyDatabase(file, destination);
                else
                    File.Copy(file, destination, overwrite: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to copy the plugin data file {File} next to the database backup {Backup}.", file, backupName);
            }
        }

        return destinationRoot;
    }
}
