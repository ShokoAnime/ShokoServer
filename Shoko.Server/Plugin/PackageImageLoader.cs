using System;
using System.IO;
using System.Reflection;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Utilities;

namespace Shoko.Server.Plugin;

/// <summary>
///   Finds and extracts the images a plugin ships: its own thumbnail and icon,
///   its metadata providers' source icons and its image contributors' icons.
/// </summary>
/// <remarks>
///   An image is a file beside the plugin, <c>&lt;kind&gt;.&lt;ext&gt;</c> in
///   its directory or <c>&lt;dll&gt;.&lt;kind&gt;.&lt;ext&gt;</c> beside a
///   loose dll, or the bytes it embedded, written out under that name so both
///   are served from a path. A file already there wins over embedded bytes.
/// </remarks>
internal static class PackageImageLoader
{
    #region Reading

    /// <summary>
    ///   Read an image a plugin embedded in its own assembly.
    /// </summary>
    /// <param name="assembly">The plugin assembly to read from.</param>
    /// <param name="assemblyName">
    ///   The assembly's name. A resource name not rooted in it is refused,
    ///   since a plugin may only name its own resources.
    /// </param>
    /// <param name="resourceName">The resource name the plugin advertised, if any.</param>
    /// <param name="kind">Which image this is, for the log line.</param>
    /// <param name="dllPath">The dll being read, for the log line.</param>
    /// <param name="logger">Logs a failed read.</param>
    /// <returns>
    ///   The image bytes, or <see langword="null"/> when the plugin advertised
    ///   none, named something outside its own assembly, or the read failed.
    /// </returns>
    public static byte[]? ReadEmbedded(Assembly assembly, string assemblyName, string? resourceName, string kind, string dllPath, ILogger logger)
    {
        if (resourceName is not { Length: > 0 } || !resourceName.StartsWith(assemblyName + "."))
            return null;

        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                logger.LogInformation("Failed to load {Kind} for {DllName}", kind, dllPath);
                return null;
            }

            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load {Kind} for {DllName}", kind, dllPath);
            return null;
        }
    }

    #endregion

    #region Loading

    /// <summary>
    ///   Find a plugin's image of the given kind: a file shipped beside the
    ///   plugin, or the bytes it embedded, which are written out so both cases
    ///   end up being served from a path.
    /// </summary>
    /// <param name="containingDirectory">
    ///   The plugin's own directory, when it has one. A plugin installed as a
    ///   loose dll does not, and names its files after the dll instead.
    /// </param>
    /// <param name="dll">The plugin's main dll.</param>
    /// <param name="imageBytes">The embedded bytes, when the plugin supplied any.</param>
    /// <param name="kind">The image's kind, which is both the file name looked for and the one written.</param>
    /// <param name="applicationPaths">Gives the folders the path is recorded against.</param>
    /// <returns>The image, or <see langword="null"/> when there is none.</returns>
    /// <exception cref="IOException">
    ///   The embedded bytes could not be written beside the plugin.
    /// </exception>
    public static PackageImageInfo? Load(string? containingDirectory, string dll, byte[]? imageBytes, string kind, IApplicationPaths applicationPaths)
    {
        var hasDirectory = !string.IsNullOrEmpty(containingDirectory);
        var directory = hasDirectory ? containingDirectory! : Path.GetDirectoryName(dll)!;
        var pattern = hasDirectory ? kind + ".*" : Path.ChangeExtension(Path.GetFileName(dll), "." + kind + ".*");
        foreach (var fileName in Directory.EnumerateFiles(directory, pattern, new EnumerationOptions() { IgnoreInaccessible = true, RecurseSubdirectories = false }))
        {
            if (!ContentTypeHelper.TryGetContentType(fileName, out _))
                continue;

            var existing = new MagickImageInfo(fileName);
            if (GetMimeFromFormat(existing) is not { } existingMime)
                continue;

            return ToImageInfo(fileName, (int)existing.Width, (int)existing.Height, existingMime, applicationPaths);
        }

        if (imageBytes is not { Length: > 8 })
            return null;

        var imageInfo = new MagickImageInfo(imageBytes);
        if (GetMimeFromFormat(imageInfo) is not { } mime)
            return null;

        if (!ContentTypeHelper.TryGetExtensionForMimeType(mime, out var extName))
            return null;

        var targetName = hasDirectory
            ? Path.Combine(containingDirectory!, kind + extName)
            : Path.ChangeExtension(dll, "." + kind + extName);
        File.WriteAllBytes(targetName, imageBytes);

        return ToImageInfo(targetName, (int)imageInfo.Width, (int)imageInfo.Height, mime, applicationPaths);
    }

    /// <summary>
    ///   Loads an icon one of a plugin's parts names: a file beside the
    ///   plugin, or the embedded image extracted there, as SVG or PNG only.
    /// </summary>
    /// <param name="pluginInfo">The plugin the icon ships with.</param>
    /// <param name="assembly">The assembly holding the embedded image.</param>
    /// <param name="resourceName">The embedded resource, if one is named.</param>
    /// <param name="kind">The image's kind, which is its file name.</param>
    /// <param name="applicationPaths">Gives the folders the path is recorded against.</param>
    /// <param name="logger">Logs an icon refused or failing to load.</param>
    /// <returns>The icon, or <see langword="null"/> when there is none or it is neither SVG nor PNG.</returns>
    public static PackageImageInfo? LoadIcon(
        LocalPluginInfo pluginInfo,
        Assembly assembly,
        string? resourceName,
        string kind,
        IApplicationPaths applicationPaths,
        ILogger logger
    )
    {
        if (pluginInfo.DLLs is not { Count: > 0 } dlls)
            return null;

        try
        {
            var bytes = ReadEmbedded(assembly, assembly.GetName().Name!, resourceName, kind, dlls[0], logger);
            var icon = Load(pluginInfo.ContainingDirectory, dlls[0], bytes, kind, applicationPaths);
            if (icon is null or { MimeType: "image/svg+xml" or "image/png" })
                return icon;

            logger.LogWarning("Ignoring the {Kind} of {Plugin}: {MimeType} is neither SVG nor PNG.", kind, pluginInfo.Name, icon.MimeType);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load the {Kind} of {Plugin}.", kind, pluginInfo.Name);
            return null;
        }
    }

    /// <summary>
    ///   Describes a found image, its path recorded against the plugins and
    ///   application folders so it survives them moving.
    /// </summary>
    /// <param name="fileName">The image's file.</param>
    /// <param name="width">The width, in pixels.</param>
    /// <param name="height">The height, in pixels.</param>
    /// <param name="mime">The mime type.</param>
    /// <param name="applicationPaths">Gives the folders the path is recorded against.</param>
    /// <returns>The image.</returns>
    private static PackageImageInfo ToImageInfo(string fileName, int width, int height, string mime, IApplicationPaths applicationPaths)
        => new()
        {
            Height = height,
            Width = width,
            FilePath = fileName
                .Replace(applicationPaths.PluginsPath, "%PluginsPath%")
                .Replace(applicationPaths.ApplicationPath, "%ApplicationPaths%"),
            MimeType = mime,
        };

    /// <summary>
    ///   The mime type of an image format a plugin may ship.
    /// </summary>
    /// <param name="imageInfo">The image.</param>
    /// <returns>The mime type, or <see langword="null"/> for any other format.</returns>
    public static string? GetMimeFromFormat(MagickImageInfo imageInfo)
        => imageInfo.Format switch
        {
            MagickFormat.Png => "image/png",
            MagickFormat.Png00 => "image/png",
            MagickFormat.Png8 => "image/png",
            MagickFormat.Png24 => "image/png",
            MagickFormat.Png32 => "image/png",
            MagickFormat.Png48 => "image/png",
            MagickFormat.Png64 => "image/png",
            MagickFormat.Jpg => "image/jpeg",
            MagickFormat.Jpeg => "image/jpeg",
            MagickFormat.WebP => "image/webp",
            MagickFormat.Svg => "image/svg+xml",
            MagickFormat.Svgz => "image/svg+xml",
            _ => null,
        };

    #endregion
}
