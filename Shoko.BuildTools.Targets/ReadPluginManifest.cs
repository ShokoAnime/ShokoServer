using System.Text.Json;
using Microsoft.Build.Framework;

namespace Shoko.BuildTools.Tasks;

/// <summary>
///   MSBuild task that reads a plugin's manifest.json and writes a .cs file
///   with assembly metadata attributes for PackageID, PackageName,
///   PackageOverview, PackageNamespace, tags, and the dependencies the
///   project states.
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

            var pluginNamespace = root.TryGetProperty("namespace", out var namespaceEl) && namespaceEl.ValueKind == JsonValueKind.String
                ? namespaceEl.GetString()
                : null;

            var tags = root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array
                ? tagsEl.EnumerateArray().Select(t => t.GetString()).OfType<string>().Where(t => t.Length > 0).ToList()
                : null;

            if (root.TryGetProperty("dependencies", out _))
                Log.LogWarning("{0} lists top-level dependencies, which a build does not read. State them in the project as PluginDependency items.", manifestPath);

            return WriteMetadata(nameof(ReadPluginManifest), id, name, overview, pluginNamespace, manifestPath, tags, null, "PluginDependencies");
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
