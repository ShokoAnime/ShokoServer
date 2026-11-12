using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Shoko.BuildTools;

/// <summary>
///   The hook targets a plugin's build can attach to around the manifest
///   write, so its targets can add to the <c>extensions</c> of a release and
///   of its archives. They run in a cascade: each asset, then the release,
///   then the write, then the release again.
/// </summary>
internal static class ManifestHooks
{
    /// <summary>
    ///   Runs once per runtime on the built project, after its archive is
    ///   packed. Hooks edit the pending archive file.
    /// </summary>
    public const string BeforeAssetWrite = "ShokoBeforeAssetWrite";

    /// <summary>
    ///   Runs once per release after every runtime, before the manifest is
    ///   saved. Hooks edit the pending release file.
    /// </summary>
    public const string BeforeReleaseWrite = "ShokoBeforeReleaseWrite";

    /// <summary>
    ///   Runs once per release after the manifest is saved.
    /// </summary>
    public const string AfterReleaseWrite = "ShokoAfterReleaseWrite";

    private const string ExtensionsKey = "extensions";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    ///   Whether anything in the project attaches to <paramref name="hook" />:
    ///   a target naming it in <c>AfterTargets</c> or <c>BeforeTargets</c>,
    ///   or a non-empty <c>&lt;hook&gt;DependsOn</c> property.
    /// </summary>
    public static bool IsAttached(ProjectInstance instance, string hook)
    {
        if (!string.IsNullOrWhiteSpace(instance.GetPropertyValue($"{hook}DependsOn")))
            return true;

        foreach (var target in instance.Targets.Values)
        {
            if (Names(instance, target.AfterTargets).Contains(hook) || Names(instance, target.BeforeTargets).Contains(hook))
                return true;
        }

        return false;

        static HashSet<string> Names(ProjectInstance instance, string value)
            => string.IsNullOrWhiteSpace(value)
                ? []
                : instance.ExpandString(value)
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///   Runs <paramref name="hook" /> on <paramref name="instance" /> with
    ///   <paramref name="properties" /> set, without building again.
    /// </summary>
    /// <returns>Whether the hook succeeded.</returns>
    public static bool Run(ProjectInstance instance, string hook, IReadOnlyDictionary<string, string> properties, LoggerVerbosity verbosity)
    {
        foreach (var (name, value) in properties)
            instance.SetProperty(name, value);

        var result = BuildManager.DefaultBuildManager.Build(
            new BuildParameters { Loggers = [new ConsoleLogger(verbosity)] },
            new BuildRequestData(instance, [hook]));
        return result.OverallResult == BuildResultCode.Success;
    }

    /// <summary>
    ///   Writes <paramref name="archive" /> to <paramref name="path" /> as the
    ///   manifest has it.
    /// </summary>
    public static void WritePendingAsset(ManifestArchive archive, string path)
        => File.WriteAllText(path, ManifestManager.ToNode(archive).ToJsonString(JsonOptions));

    /// <summary>
    ///   Writes <paramref name="release" />, with all its archives, to
    ///   <paramref name="path" /> as the manifest has it.
    /// </summary>
    public static void WritePendingRelease(ManifestRelease release, string path)
        => File.WriteAllText(path, ManifestManager.ToNode(release).ToJsonString(JsonOptions));

    /// <summary>
    ///   Reads the pending archive back from <paramref name="path" /> and
    ///   merges its <c>extensions</c> into <paramref name="archive" />. Every
    ///   other field is left as the build wrote it.
    /// </summary>
    /// <returns>Why the file could not be merged, or <c>null</c> when it was.</returns>
    public static string? MergePendingAsset(ManifestArchive archive, string path)
    {
        JsonObject? extensions;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject pending)
                return "it is not a JSON object";

            if (!TryGetExtensions(pending, "the archive", out extensions, out var error))
                return error;
        }
        // An object's properties are read lazily, so a duplicate key throws an
        // ArgumentException on first access rather than a JsonException on parse.
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return $"it is not valid JSON: {ex.Message}";
        }

        if (extensions is not null)
            archive.Extra = Merge(archive.Extra, extensions);

        return null;
    }

    /// <summary>
    ///   Reads the pending release back from <paramref name="path" /> and
    ///   merges its <c>extensions</c>, and those of each of its archives
    ///   matched by runtime, into <paramref name="release" />. Every other
    ///   field is left as the build wrote it.
    /// </summary>
    /// <returns>Why the file could not be merged, or <c>null</c> when it was.</returns>
    public static string? MergePendingRelease(ManifestRelease release, string path)
    {
        JsonObject? releaseExtensions;
        var archiveExtensions = new List<(ManifestArchive Archive, JsonObject Extensions)>();
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject pending)
                return "it is not a JSON object";

            if (!TryGetExtensions(pending, "the release", out releaseExtensions, out var error))
                return error;

            foreach (var pendingArchive in (pending["archives"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (pendingArchive["runtime"] is not JsonValue value || !value.TryGetValue<string>(out var runtime))
                    continue;

                if (release.Archives?.FirstOrDefault(a => a.Runtime == runtime) is not { } archive)
                    continue;

                if (!TryGetExtensions(pendingArchive, $"the {runtime} archive", out var extensions, out error))
                    return error;

                if (extensions is not null)
                    archiveExtensions.Add((archive, extensions));
            }
        }
        // See MergePendingAsset.
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return $"it is not valid JSON: {ex.Message}";
        }

        if (releaseExtensions is not null)
            release.Extra = Merge(release.Extra, releaseExtensions);

        foreach (var (archive, extensions) in archiveExtensions)
            archive.Extra = Merge(archive.Extra, extensions);

        return null;
    }

    /// <summary>
    ///   Merges <paramref name="source" /> into <paramref name="target" />
    ///   key by key: objects merge, a <c>null</c> removes the key, anything
    ///   else replaces it.
    /// </summary>
    internal static void MergeObjects(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source.ToList())
        {
            if (value is null)
                target.Remove(key);
            else if (value is JsonObject sourceChild && target[key] is JsonObject targetChild)
                MergeObjects(targetChild, sourceChild);
            else
                target[key] = value.DeepClone();
        }
    }

    private static bool TryGetExtensions(JsonObject owner, string what, out JsonObject? extensions, out string? error)
    {
        extensions = null;
        error = null;
        if (!owner.TryGetPropertyValue(ExtensionsKey, out var node) || node is null)
            return true;

        if (node is not JsonObject value)
        {
            error = $"the extensions of {what} are not a JSON object";
            return false;
        }

        extensions = value;
        return true;
    }

    private static Dictionary<string, JsonElement>? Merge(Dictionary<string, JsonElement>? extra, JsonObject incoming)
    {
        var current = extra is not null && extra.TryGetValue(ExtensionsKey, out var element) && element.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(element.GetRawText())!.AsObject()
            : [];

        MergeObjects(current, incoming);

        extra ??= [];
        if (current.Count == 0)
            extra.Remove(ExtensionsKey);
        else
            extra[ExtensionsKey] = JsonSerializer.SerializeToElement(current);

        return extra.Count == 0 ? null : extra;
    }
}
