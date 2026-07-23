using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Locator;

namespace Shoko.BuildTools;

internal static class BuildCommand
{
    public static async Task<int> RunAsync(
        string? manifestPath,
        int? pruneCount,
        string pruneMethod,
        string? downloadUrl,
        string? outputZip,
        string[] forwardArgs)
    {
        try
        {
            var projectDir = Directory.GetCurrentDirectory();
            var manifestFullPath = manifestPath is not null
                ? Path.GetFullPath(manifestPath, projectDir)
                : LookForManifestNearby(projectDir);

            // ── Discover the project to build ─────────────────────────
            var projectFile = ResolveProjectFile(projectDir, forwardArgs);
            if (projectFile is null)
            {
                Console.Error.WriteLine("No project file (.csproj) found.");
                return 1;
            }

            // ── Read manifest for metadata ────────────────────────────
            var manifest = manifestFullPath is not null && File.Exists(manifestFullPath)
                ? ManifestManager.Load(manifestFullPath)
                : null;

            var dependencies = manifest?.Dependencies
                ?.Select(d => new DependencyInfo
                {
                    PluginID = d.ID,
                    VersionRange = d.Version,
                    IsOptional = d.IsOptional,
                })
                .ToList() ?? [];

            var manifestTags = manifest?.Tags;
            var manifestPluginId = manifest?.ID;
            var manifestPluginName = manifest?.Name;
            var manifestPluginDescription = manifest?.Overview;

            // ── Gather git metadata ───────────────────────────────────
            var repoUrl = await RunGitAsync("remote get-url origin");
            var releaseDate = (await RunGitAsync("log -1 --format=%aI")).Trim();
            var sourceRevision = (await RunGitAsync("rev-parse HEAD")).Trim();
            var releaseTag = (await RunGitAsync("describe --exact-match --tags --match \"v[0-9]*.[0-9]*.[0-9]*\"")).Trim();

            // ── Register MSBuild ──────────────────────────────────────
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();

            // ── Load .csproj and modify in memory ─────────────────────
            var projectCollection = new ProjectCollection();
            var project = new Project(projectFile, null, null, projectCollection);

            var targets = new List<string> { "Build" };
            ApplyForwardArgs(project, forwardArgs, targets);

            // Determine which RIDs to build. If RuntimeIdentifiers is set
            // (semicolon-separated), build each one. Otherwise use the
            // single RuntimeIdentifier or "any".
            var ridListRaw = project.GetPropertyValue("RuntimeIdentifiers");
            var rids = !string.IsNullOrEmpty(ridListRaw)
                ? ridListRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : [];

            if (rids.Count == 0)
            {
                var single = project.GetPropertyValue("RuntimeIdentifier");
                rids = [string.IsNullOrEmpty(single) ? "any" : single];
            }

            // ── Source probe: auto-detect tags ────────────────────────
            var existingTagSet = manifestTags?.ToHashSet() ?? [];
            var autoTags = SourceProbe.DiscoverTags(project, projectDir, existingTagSet);

            if (autoTags.Count > 0)
            {
                manifestTags ??= [];
                foreach (var tag in autoTags)
                {
                    if (!manifestTags.Contains(tag))
                        manifestTags.Insert(0, tag);
                }

                Console.WriteLine($"Discovered tags: {string.Join(", ", autoTags)}");

                if (manifest is not null)
                {
                    manifest.Tags ??= [];
                    foreach (var tag in autoTags)
                    {
                        if (!manifest.Tags.Contains(tag))
                            manifest.Tags.Insert(0, tag);
                    }
                }
            }

            // ── Generate and write targets file (shared across RIDs) ──
            var objDir = Path.Combine(projectDir, "obj", "Shoko.BuildTools");
            Directory.CreateDirectory(objDir);
            var targetsFile = Path.Combine(objDir, "Shoko.BuildTools.targets");
            await WriteTargetsFileAsync(targetsFile);

            var pluginName = manifestPluginName?.Replace(" ", "") ?? "plugin";
            var success = false;

            foreach (var rid in rids)
            {
                Console.WriteLine($"Building for {rid}...");

                // Fresh project instance per RID to pick up the correct properties
                var ridCollection = new ProjectCollection();
                var ridProject = new Project(projectFile, null, null, ridCollection);
                ApplyForwardArgs(ridProject, forwardArgs, targets);
                ridProject.SetProperty("RuntimeIdentifier", rid);
                ridProject.SetProperty("EnableDynamicLoading", "true");

                // Generate assembly info with this RID
                var assemblyInfoFile = Path.Combine(objDir, $"Shoko.Build.AssemblyInfo.{SanitizeRid(rid)}.cs");
                AssemblyInfoGenerator.WriteAssemblyInfo(
                    assemblyInfoFile,
                    rid,
                    releaseDate,
                    releaseTag,
                    sourceRevision,
                    dependencies,
                    manifestTags,
                    manifestPluginId,
                    manifestPluginName,
                    manifestPluginDescription);

                ridProject.SetProperty("ShokoBuildAssemblyInfoFile", assemblyInfoFile);
                ridProject.Xml.AddImport(targetsFile);

                var instance = ridProject.CreateProjectInstance();
                var buildParams = new BuildParameters();
                var requestData = new BuildRequestData(instance, [.. targets]);

                var result = BuildManager.DefaultBuildManager.Build(buildParams, requestData);
                ridCollection.UnloadAllProjects();
                ridCollection.Dispose();

                if (result.OverallResult != BuildResultCode.Success)
                {
                    Console.Error.WriteLine($"Build failed for {rid}.");
                    continue;
                }

                success = true;

                // ── Post-build: pack zip, checksum, update manifest ───
                var builtDll = FindBuiltDll(projectDir, instance);
                if (builtDll is null)
                {
                    Console.Error.WriteLine($"No plugin DLL found for {rid}.");
                    continue;
                }

                var metadata = AssemblyInfoGenerator.ReadAssemblyMetadata(builtDll);
                var versionStr = $"{metadata.Version.Major}.{metadata.Version.Minor}.{metadata.Version.Build}";
                var abstractionStr = $"{metadata.AbstractionVersion.Major}.{metadata.AbstractionVersion.Minor}.{metadata.AbstractionVersion.Build}";

                // Resolve output path with template substitution
                var subst = new Dictionary<string, string>
                {
                    ["runtime"] = rid,
                    ["version"] = versionStr,
                    ["name"] = pluginName,
                    ["abstraction"] = abstractionStr,
                };
                var defaultName = $"{pluginName}-v{versionStr}-{abstractionStr}-{rid}.zip";
                var zipPath = outputZip is not null
                    ? Path.GetFullPath(Substitute(outputZip, subst))
                    : Path.Combine(projectDir, defaultName);

                // Pack the build output into a zip
                var binDir = Path.GetDirectoryName(builtDll)!;
                var publishDir = Path.Combine(binDir, "publish");
                var sourceDir = Directory.Exists(publishDir) ? publishDir : binDir;

                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                ZipFile.CreateFromDirectory(sourceDir, zipPath);

                // Compute SHA256 checksum
                string checksum;
                await using (var stream = File.OpenRead(zipPath))
                {
                    var hash = await SHA256.HashDataAsync(stream);
                    checksum = $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
                }

                Console.WriteLine($"Packed: {zipPath}");
                Console.WriteLine($"  Checksum: {checksum}");

                // Infer download URL with template substitution
                var archiveUrl = downloadUrl is not null ? Substitute(downloadUrl, subst) : null;
                if (string.IsNullOrEmpty(archiveUrl))
                {
                    var tag = !string.IsNullOrEmpty(releaseTag) ? releaseTag : $"v{versionStr}";

                    // Try git remote first, then manifest's repository_url
                    var remoteTuple = ParseGitRemote(repoUrl) ?? ParseGitRemote(manifest?.RepositoryUrl);

                    if (remoteTuple is not null)
                    {
                        var (host, owner, repo) = remoteTuple.Value;

                        archiveUrl = host switch
                        {
                            "github.com" => $"https://github.com/{owner}/{repo}/releases/download/{tag}/{defaultName}",
                            "gitea.com" => $"https://gitea.com/{owner}/{repo}/releases/download/{tag}/{defaultName}",
                            "codeberg.org" => $"https://codeberg.org/{owner}/{repo}/releases/download/{tag}/{defaultName}",
                            "gitlab.com" => $"https://gitlab.com/{owner}/{repo}/-/releases/{tag}/downloads/{defaultName}",
                            "bitbucket.org" => $"https://bitbucket.org/{owner}/{repo}/downloads/{defaultName}",
                            _ => $"https://{host}/{owner}/{repo}/releases/download/{tag}/{defaultName}",
                        };
                    }
                }

                // Update manifest with archive entry
                if (manifest is not null && manifestFullPath is not null)
                {
                    ManifestManager.UpdateOrAddRelease(
                        manifest,
                        metadata.Version,
                        metadata.AbstractionVersion,
                        rid,
                        metadata.Dependencies,
                        archiveUrl,
                        checksum);
                }
            }

            // ── Save manifest once after all RIDs ─────────────────────
            if (manifest is not null && manifestFullPath is not null)
            {
                if (pruneCount.HasValue)
                    ManifestManager.PruneReleases(manifest, pruneCount.Value, pruneMethod);

                ManifestManager.Save(manifest, manifestFullPath);
                Console.WriteLine($"Updated manifest: {manifestFullPath}");
            }

            return success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Build failed: {ex.Message}");
            return 1;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string Substitute(string template, Dictionary<string, string> values)
    {
        var result = template;
        foreach (var (key, value) in values)
            result = result.Replace($"{{{key}}}", value);
        return result;
    }

    private static string SanitizeRid(string rid)
        => rid.Replace('-', '_').Replace('.', '_');

    private static (string host, string owner, string repo)? ParseGitRemote(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();

        string host, path;

        if (url.StartsWith("git@"))
        {
            // git@github.com:owner/repo.git
            var parts = url.Split(':');
            if (parts.Length < 2)
                return null;

            host = parts[0].Replace("git@", "");
            path = parts[1];
        }
        else if (url.StartsWith("https://") || url.StartsWith("http://"))
        {
            // https://github.com/owner/repo.git
            var uri = new Uri(url);
            host = uri.Host;
            path = uri.PathAndQuery.Trim('/');
        }
        else
        {
            return null;
        }

        // Strip trailing .git
        if (path.EndsWith(".git"))
            path = path[..^4];

        // Split into owner/repo
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return null;

        return (host, segments[^2], segments[^1]);
    }

    private static string? LookForManifestNearby(string projectDir)
    {
        var cwd = Directory.GetCurrentDirectory();
        var cwdManifest = Path.Combine(cwd, "manifest.json");
        if (File.Exists(cwdManifest))
            return cwdManifest;

        if (!string.Equals(cwd, projectDir, StringComparison.Ordinal))
        {
            var dirManifest = Path.Combine(projectDir, "manifest.json");
            if (File.Exists(dirManifest))
                return dirManifest;
        }

        var parentDir = Path.GetDirectoryName(projectDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (parentDir is not null && !string.Equals(parentDir, cwd, StringComparison.Ordinal))
        {
            var parentManifest = Path.Combine(parentDir, "manifest.json");
            if (File.Exists(parentManifest))
                return parentManifest;
        }

        return null;
    }

    private static string? ResolveProjectFile(string projectDir, string[] forwardArgs)
    {
        foreach (var arg in forwardArgs)
        {
            if (arg.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(arg, projectDir);
            if (arg.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(arg, projectDir);
        }

        var csprojFiles = Directory.GetFiles(projectDir, "*.csproj");
        if (csprojFiles.Length == 1)
            return csprojFiles[0];

        return null;
    }

    private static void ApplyForwardArgs(
        Project project,
        string[] forwardArgs,
        List<string> targets)
    {
        for (var i = 0; i < forwardArgs.Length; i++)
        {
            var arg = forwardArgs[i];

            if (arg.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                arg.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                continue;

            if ((arg.StartsWith("-p:") || arg.StartsWith("/p:")) && arg.Contains('='))
            {
                var eq = arg.IndexOf('=');
                var key = arg[3..eq];
                var value = arg[(eq + 1)..];
                project.SetProperty(key, value);
                continue;
            }

            if (arg.Equals("build", StringComparison.OrdinalIgnoreCase)) continue;
            if (arg.Equals("rebuild", StringComparison.OrdinalIgnoreCase))
            {
                targets.Clear(); targets.Add("Rebuild");
                continue;
            }
            if (arg.Equals("clean", StringComparison.OrdinalIgnoreCase))
            {
                targets.Clear(); targets.Add("Clean");
                continue;
            }

            if ((arg is "-c" or "--configuration") && i + 1 < forwardArgs.Length)
            { project.SetProperty("Configuration", forwardArgs[++i]); continue; }

            if ((arg is "-f" or "--framework") && i + 1 < forwardArgs.Length)
            { project.SetProperty("TargetFramework", forwardArgs[++i]); continue; }

            if ((arg is "-r" or "--runtime") && i + 1 < forwardArgs.Length)
            { project.SetProperty("RuntimeIdentifier", forwardArgs[++i]); continue; }

            if ((arg is "-o" or "--output") && i + 1 < forwardArgs.Length)
            { project.SetProperty("OutputPath", forwardArgs[++i]); continue; }

            if (arg is "--no-restore")
            { project.SetProperty("RestorePackages", "false"); continue; }

            if (arg is "--no-build")
            { project.SetProperty("BuildProject", "false"); continue; }

            if (arg is "--no-dependencies")
            { project.SetProperty("BuildProjectReferences", "false"); continue; }
        }
    }

    private static async Task<string> RunGitAsync(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process is null) return "";
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output.Trim();
        }
        catch { return ""; }
    }

    private static string? FindBuiltDll(string projectDir, ProjectInstance instance)
    {
        var config = instance.GetPropertyValue("Configuration");
        if (string.IsNullOrEmpty(config)) config = "Debug";

        var tfm = instance.GetPropertyValue("TargetFramework");
        if (string.IsNullOrEmpty(tfm)) tfm = "net10.0";

        var rid = instance.GetPropertyValue("RuntimeIdentifier");

        // Try RID-specific bin dir first, then fall back
        var candidates = new List<string>();
        var outputPath = instance.GetPropertyValue("OutputPath");
        if (!string.IsNullOrEmpty(outputPath))
            candidates.Add(Path.GetFullPath(outputPath, projectDir));

        if (!string.IsNullOrEmpty(rid) && rid != "any")
            candidates.Add(Path.Combine(projectDir, "bin", config, tfm, rid));

        candidates.Add(Path.Combine(projectDir, "bin", config, tfm));

        foreach (var dir in candidates)
        {
            if (!Directory.Exists(dir)) continue;
            var dll = Directory.GetFiles(dir, "*.dll")
                .Where(dll =>
                {
                    var name = Path.GetFileNameWithoutExtension(dll);
                    return !name.StartsWith("System") &&
                           !name.StartsWith("Microsoft") &&
                           !name.StartsWith("Shoko.Abstractions") &&
                           !name.StartsWith("Shoko.QueueProcessor") &&
                           !name.StartsWith("Shoko.Server");
                })
                .MaxBy(dll => new FileInfo(dll).Length);
            if (dll is not null) return dll;
        }

        return null;
    }

    private static async Task WriteTargetsFileAsync(string path)
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Shoko.BuildTools.PluginAssemblyInfo.targets";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is not null)
        {
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            await File.WriteAllTextAsync(path, content);
            return;
        }

        var repoRoot = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "..", "..", "..", ".."));
        var sourcePath = Path.Combine(repoRoot, "Shoko.BuildTools", "PluginAssemblyInfo.targets");
        if (File.Exists(sourcePath))
        {
            var content = await File.ReadAllTextAsync(sourcePath);
            await File.WriteAllTextAsync(path, content);
        }
    }
}
