using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Enums;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;

namespace Shoko.Server.Services;

/// <summary>
///   Reads the <c>third-party-notices.json.br</c> files of the core and the
///   loaded plugins. Each file is read once and again only when it changes on
///   disk.
/// </summary>
/// <param name="pluginManager">Lists the loaded plugins and their folders.</param>
/// <param name="applicationPaths">Where the core and its web UI live.</param>
/// <param name="logger">The logger.</param>
public class ThirdPartyNoticeService(IPluginManager pluginManager, IApplicationPaths applicationPaths, ILogger<ThirdPartyNoticeService> logger) : IThirdPartyNoticeService
{
    /// <summary>
    ///   The notices file's name.
    /// </summary>
    internal const string FileName = "third-party-notices.json.br";

    /// <summary>
    ///   The newest format version understood. A newer file is still read, for
    ///   the members it shares.
    /// </summary>
    private const int FormatVersion = 1;

    private readonly Lock _lock = new();

    private readonly Dictionary<string, CachedFile> _files = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyList<ThirdPartyNoticeSection> GetSections()
    {
        var sections = new List<ThirdPartyNoticeSection>();
        foreach (var (plugin, files) in NoticeFiles())
        {
            var parsed = files.Select(Read).OfType<NoticesFile>().ToList();
            if (parsed.Count is 0)
                continue;

            var packages = parsed.SelectMany(file => file.Packages)
                .DistinctBy(notice => (notice.ID.ToLowerInvariant(), notice.Version?.ToLowerInvariant(), notice.Source))
                .OrderBy(notice => notice.ID, StringComparer.OrdinalIgnoreCase)
                .ThenBy(notice => notice.Version, StringComparer.Ordinal)
                .ToList();
            var project = parsed.Select(file => file.Project).OfType<ThirdPartyNotice>().FirstOrDefault();
            if (project is null && packages.Count is 0)
                continue;

            sections.Add(new()
            {
                PluginID = plugin.ID,
                PluginName = plugin.Name,
                PluginVersion = plugin.Version.Version,
                IsCore = plugin.ID == CorePlugin.StaticID,
                Project = project,
                Packages = packages,
            });
        }

        return sections;
    }

    /// <inheritdoc />
    public string? GetText(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        foreach (var (_, files) in NoticeFiles())
            foreach (var file in files)
                if (Read(file)?.Texts.TryGetValue(key, out var text) is true)
                    return text;

        return null;
    }

    /// <summary>
    ///   The notices files of the core and of each active plugin with a folder
    ///   of its own, the core first, then load order: the one beside the
    ///   plugin's assembly, and for the core also its web UI's.
    /// </summary>
    private IEnumerable<(LocalPluginInfo Plugin, IReadOnlyList<string> Files)> NoticeFiles()
    {
        var plugins = pluginManager.GetPluginInfos()
            .Where(plugin => plugin.IsActive)
            .OrderBy(plugin => plugin.ID == CorePlugin.StaticID ? 0 : 1)
            .ThenBy(plugin => plugin.LoadOrder);
        foreach (var plugin in plugins)
        {
            var files = new List<string>();
            if (plugin.ID == CorePlugin.StaticID)
            {
                files.Add(Path.Combine(applicationPaths.ApplicationPath, FileName));
                files.Add(Path.Combine(applicationPaths.ApplicationPath, "webui", FileName));
                files.Add(Path.Combine(applicationPaths.DataPath, "webui", FileName));
            }
            else if (plugin.ContainingDirectory is { } directory)
            {
                files.Add(Path.Combine(directory, FileName));
            }

            yield return (plugin, files);
        }
    }

    /// <summary>
    ///   Reads a notices file, or answers what was read before when it has not
    ///   changed since.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The notices, or <c>null</c> when the file is missing or unreadable.</returns>
    private NoticesFile? Read(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            return null;

        lock (_lock)
        {
            if (_files.TryGetValue(path, out var cached) && cached.Length == info.Length && cached.WrittenAt == info.LastWriteTimeUtc)
                return cached.Notices;

            NoticesFile? notices = null;
            try
            {
                using var stream = info.OpenRead();
                using var brotli = new BrotliStream(stream, CompressionMode.Decompress);
                using var document = JsonDocument.Parse(brotli);
                notices = Parse(document.RootElement);
                if (notices is null)
                    logger.LogWarning("The third-party notices in {Path} are not in a format the server reads.", path);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or JsonException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not read the third-party notices in {Path}.", path);
            }

            _files[path] = new(info.Length, info.LastWriteTimeUtc, notices);
            return notices;
        }
    }

    /// <summary>
    ///   Reads the members of a notices file the server understands.
    /// </summary>
    /// <param name="root">The file's root object.</param>
    /// <returns>The notices, or <c>null</c> when it is not a notices file.</returns>
    internal static NoticesFile? Parse(JsonElement root)
    {
        if (root.ValueKind is not JsonValueKind.Object ||
            !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number < FormatVersion)
            return null;

        var packages = new List<ThirdPartyNotice>();
        if (root.TryGetProperty("packages", out var list) && list.ValueKind is JsonValueKind.Array)
            foreach (var element in list.EnumerateArray())
                if (Entry(element) is { } entry)
                    packages.Add(entry);

        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("texts", out var table) && table.ValueKind is JsonValueKind.Object)
            foreach (var text in table.EnumerateObject())
                if (text.Value.ValueKind is JsonValueKind.String && text.Value.GetString() is { Length: > 0 } value)
                    texts[text.Name] = value;

        var project = root.TryGetProperty("project", out var own) ? Entry(own) : null;
        return new(project, packages, texts);
    }

    private static ThirdPartyNotice? Entry(JsonElement element)
    {
        if (element.ValueKind is not JsonValueKind.Object || String(element, "id") is not { } id)
            return null;

        var keys = element.TryGetProperty("textKeys", out var list) && list.ValueKind is JsonValueKind.Array
            ? list.EnumerateArray().Where(key => key.ValueKind is JsonValueKind.String).Select(key => key.GetString()!).Where(key => key.Length > 0).ToList()
            : [];
        return new()
        {
            ID = id,
            Version = String(element, "version"),
            Source = String(element, "source")?.ToLowerInvariant() switch
            {
                "nuget" => ThirdPartyNoticeSource.NuGet,
                "npm" => ThirdPartyNoticeSource.Npm,
                "asset" => ThirdPartyNoticeSource.Asset,
                "project" => ThirdPartyNoticeSource.Project,
                _ => ThirdPartyNoticeSource.Generic,
            },
            License = String(element, "license"),
            Copyright = String(element, "copyright"),
            Url = String(element, "url"),
            TextKeys = keys,
        };
    }

    private static string? String(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    /// <summary>
    ///   The members of a notices file the server reads.
    /// </summary>
    /// <param name="Project">The plugin's own licence, if given.</param>
    /// <param name="Packages">The packages and assets.</param>
    /// <param name="Texts">The texts by key.</param>
    internal sealed record NoticesFile(ThirdPartyNotice? Project, IReadOnlyList<ThirdPartyNotice> Packages, IReadOnlyDictionary<string, string> Texts);

    private sealed record CachedFile(long Length, DateTime WrittenAt, NoticesFile? Notices);
}
