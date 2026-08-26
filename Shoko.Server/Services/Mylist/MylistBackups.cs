using System;
using System.IO;
using Shoko.Abstractions.Plugin;

namespace Shoko.Server.Services.Mylist;

/// <summary>
/// Where MyList backups live and what they are called. Rotation matches on the
/// name, so the convention is kept in one place rather than restated wherever a
/// backup is written.
/// </summary>
internal static class MylistBackups
{
    /// <summary>
    /// Matches only names starting with an ISO 8601 date, so nothing else in the
    /// directory is ever a rotation candidate.
    /// </summary>
    public const string RotationPattern = "????-??-?? *.json.gz";

    /// <summary>
    /// A separate directory, so a backup can never collide with the working
    /// cache.
    /// </summary>
    public static DirectoryInfo DirectoryFor(IApplicationPaths applicationPaths)
        => new(Path.Combine(applicationPaths.DataPath, "MyList", "Backups"));

    /// <summary>
    /// Rotation sorts on the filename, so the timestamp leads and is written in
    /// the universally sortable ("u") format, with the colons swapped for
    /// something a filesystem will accept.
    /// </summary>
    /// <param name="backedUpAt">
    ///   When the backup was taken.
    /// </param>
    /// <param name="suffix">
    ///   An optional word after the timestamp, for a backup worth telling apart
    ///   from the rest.
    /// </param>
    public static string NameFor(DateTimeOffset backedUpAt, string? suffix = null)
        => backedUpAt.ToString("u").Replace(':', '_') + (suffix is null ? string.Empty : " " + suffix) + ".json.gz";
}
