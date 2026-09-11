using System.Text.Json;
using Microsoft.Build.Framework;

namespace Shoko.BuildTools.Tasks;

/// <summary>
///   MSBuild task that reads a plugin's manifest.json and writes a .cs file
///   with assembly metadata attributes for PackageID, PackageName,
///   PackageOverview, tags, and dependencies.
/// </summary>
public class ReadPluginManifest : PluginMetadataTask
{
    public override bool Execute()
    {
        try
        {
            var manifestPath = FindManifest(ProjectDir);
            if (manifestPath is null)
            {
                Log.LogMessage(MessageImportance.Low, "No manifest.json found near {0}", ProjectDir);
                return true;
            }

            Log.LogMessage(MessageImportance.Normal, "Reading plugin manifest: {0}", manifestPath);

            var json = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var id = (Guid?)null;
            if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
            {
                if (!Guid.TryParse(idEl.GetString(), out var parsedId))
                {
                    Log.LogError("The id in {0}, '{1}', is not a GUID.", manifestPath, idEl.GetString());
                    return false;
                }

                id = parsedId;
            }

            var name = root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()
                : null;

            var overview = root.TryGetProperty("overview", out var descEl) && descEl.ValueKind == JsonValueKind.String
                ? descEl.GetString()
                : null;

            var tags = root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array
                ? tagsEl.EnumerateArray().Select(t => t.GetString()).OfType<string>().Where(t => t.Length > 0).ToList()
                : null;

            var dependencies = new List<PluginDependencyEntry>();
            if (root.TryGetProperty("dependencies", out var depsEl) && depsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var d in depsEl.EnumerateArray())
                {
                    if (!d.TryGetProperty("id", out var depIdEl) || !Guid.TryParse(depIdEl.GetString(), out var depId) ||
                        !d.TryGetProperty("version", out var depVersionEl) || depVersionEl.GetString() is not { } depVersion)
                    {
                        Log.LogError("A dependency in {0} lacks a GUID id or a version: {1}", manifestPath, d.GetRawText());
                        return false;
                    }

                    var optional = d.TryGetProperty("optional", out var o) && o.ValueKind == JsonValueKind.True;
                    dependencies.Add(new PluginDependencyEntry(depId, depVersion, optional));
                }
            }

            return WriteMetadata(nameof(ReadPluginManifest), id, name, overview, tags, dependencies, $"dependencies in {manifestPath}");
        }
        catch (Exception ex)
        {
            Log.LogError("Failed to read plugin manifest: {0}", ex.Message);
            return false;
        }
    }

    private static string? FindManifest(string projectDir)
    {
        var dir = projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var local = Path.Combine(dir, "manifest.json");
        if (File.Exists(local))
            return local;

        var parent = Path.GetDirectoryName(dir);
        if (parent is not null)
        {
            var parentManifest = Path.Combine(parent, "manifest.json");
            if (File.Exists(parentManifest))
                return parentManifest;
        }

        return null;
    }
}
