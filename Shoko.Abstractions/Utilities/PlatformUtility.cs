using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Extensions;

namespace Shoko.Abstractions.Utilities;

/// <summary>
///   Utility methods for working with file and directory paths.
/// </summary>
public static partial class PlatformUtility
{
    [GeneratedRegex(@"^(?<letter>[a-zA-Z]):\\?|^win://(?<letter>[a-zA-Z])/?")]
    private static partial Regex DriveLetterRegex();

    /// <summary>
    ///   Indicates that the current system is part of the Windows family of
    ///   platforms. This includes all versions of Windows, and Xbox.
    /// </summary>
    public static bool IsWindows => OperatingSystem.IsWindows();

    /// <summary>
    ///   Indicates that the current system is part of the Linux family of
    ///   platforms.
    /// </summary>
    public static bool IsLinux => OperatingSystem.IsLinux();

    /// <summary>
    ///   Indicates that the current system is part of the FreeBSD family of
    ///   platforms.
    /// </summary>
    public static bool IsFreeBSD => OperatingSystem.IsFreeBSD();

    /// <summary>
    ///   Indicates that the current system is part of the MacOS family of
    ///   platforms.
    /// </summary>
    public static bool IsMacOS => OperatingSystem.IsMacOS();

    /// <summary>
    ///   Indicates that the current system is an Unix-like platform. This includes Linux, MacOS, and FreeBSD.
    /// </summary>
    public static bool IsUnixLike => OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD() || OperatingSystem.IsMacOS();

    /// <summary>
    ///   The string comparison to use for paths on the current platform.
    /// </summary>
    public static readonly StringComparison StringComparison = IsWindows
        ? StringComparison.InvariantCultureIgnoreCase
        : StringComparison.InvariantCulture;

    /// <summary>
    ///   The string comparer to use for paths on the current platform.
    /// </summary>
    public static readonly StringComparer StringComparer = IsWindows
        ? StringComparer.InvariantCultureIgnoreCase
        : StringComparer.InvariantCulture;

    /// <summary>
    ///   Ensures that a path is usable on the current platform.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The potentially modified path.</returns>
    public static string EnsureUsablePath(string path)
    {
        if (!IsWindows)
            return path;
        // Use long paths if we're on Windows and the path is longer than 250
        // characters.
        if (path.Length > 250)
            path = path.StartsWith(@"\\") && !path.StartsWith(@"\\?\")
                ? @"\\?\UNC" + path[1..]
                : @"\\?\" + path;
        return path.Replace('/', '\\');
    }

    /// <summary>
    ///   Normalizes the path into an universal format which can be used on all
    ///   platforms and which is consistent across platforms, except if
    ///   <paramref name="platformFormat"/> is set to
    ///   <see langword="true"/>, in which case it normalizes the path into an
    ///   universal format for the current platform.
    /// </summary>
    /// <param name="path">
    ///   The path to normalize.
    /// </param>
    /// <param name="platformFormat">
    ///   Determines if the path should use the platform specific format.
    /// </param>
    /// <param name="stripLeadingSlash">
    ///   Strips the leading slash from the path.
    /// </param>
    /// <returns>
    ///   The normalized path.
    /// </returns>
    public static string NormalizePath(string? path, bool platformFormat = false, bool stripLeadingSlash = false)
    {
        // Ensure the path is not null.
        path ??= string.Empty;

        // Remove the file:// prefix if it's present.
        if (path.StartsWith("file://"))
            path = path[7..];

        // Determine if the path is in long path format, if it's an UNC path, and which drive letter it belongs to.
        var isLongPath = IsWindows && path.StartsWith(@"\\?\");
        if (isLongPath)
            path = path[4..];
        var isNetworkShare = IsWindows
            ? (isLongPath ? path.StartsWith(@"UNC\") : path.StartsWith(@"\\"))
            : path.StartsWith("smb://");
        if (isNetworkShare)
            path = IsWindows ? isLongPath ? path[4..] : path[2..] : path[6..];
        var driveLetter = (string?)null;
        if (!isNetworkShare && DriveLetterRegex().Match(path) is { Success: true } driveLetterResult)
        {
            path = path[driveLetterResult.Length..];
            driveLetter = driveLetterResult.Groups["letter"].Value.ToUpperInvariant();
        }

        // Normalize and traverse the path.
        path = path.Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        var isAbsolute = path.StartsWith('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new Stack<string>();
        foreach (var segment in segments)
        {
            if (segment is ".")
                continue;
            if (segment is "..")
            {
                if (stack.Count > 0) stack.Pop();
                continue;
            }
            stack.Push(segment);
        }
        path = (isAbsolute ? "/" : "") + stack.Reverse().Join('/');

        // If the path is empty and there's no drive letter, just return it.
        if (string.IsNullOrWhiteSpace(path) && driveLetter is null)
            return path;

        // Platform specific universal format.
        if (platformFormat)
        {
            var sep = Path.DirectorySeparatorChar;
            path = path.Replace('/', sep);
            if (isNetworkShare)
                return IsWindows ? $@"\\{path}" : $@"smb://{path}";

            if (driveLetter is not null)
            {
                var needsSlash = path.Length > 0 && path[0] != sep;
                return IsWindows
                    ? $"{driveLetter}:{(needsSlash ? sep + path : path)}"
                    : $"win://{driveLetter}{(needsSlash ? sep + path : path)}";
            }

            if (stripLeadingSlash && path.StartsWith(Path.DirectorySeparatorChar))
                return path[1..];

            return path;
        }

        // Internal universal format.
        if (isNetworkShare)
            return $@"smb://{path}";
        if (driveLetter is not null)
            return $@"win://{driveLetter}{(path is not "" && !path.StartsWith('/') ? '/' + path : path)}";
        if (stripLeadingSlash && path.StartsWith('/'))
            return path[1..];
        return path;
    }
}
