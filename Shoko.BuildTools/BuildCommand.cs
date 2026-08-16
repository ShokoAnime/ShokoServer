using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.Build.Logging;

namespace Shoko.BuildTools;

internal static class BuildCommand
{
    public static async Task<int> RunAsync(
        string? manifestPath,
        int? pruneCount,
        string pruneMethod,
        string downloadUrl,
        string outputZip,
        string? channel,
        string? tagOverride,
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
            var releaseDate = (await RunGitAsync("log -1 --format=%aI")).Trim();
            var sourceRevision = (await RunGitAsync("rev-parse HEAD")).Trim();
            var releaseTag = !string.IsNullOrWhiteSpace(tagOverride)
                ? tagOverride.Trim()
                : (await RunGitAsync("describe --exact-match --tags --match \"v[0-9]*.[0-9]*.[0-9]*\"")).Trim();

            // ── Register MSBuild ──────────────────────────────────────
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();

            // ── Load .csproj and modify in memory ─────────────────────
            var targets = new List<string> { "Build" };
            var restore = true;
            var properties = CollectForwardArgs(forwardArgs, targets, ref restore);

            var projectCollection = new ProjectCollection(properties);
            var project = new Project(projectFile, properties, null, projectCollection);

            // ── Release channel ───────────────────────────────────────
            var propertyChannel = project.GetPropertyValue("ReleaseChannel");
            var resolvedChannel = (ReleaseChannel?)null;

            if (!string.IsNullOrWhiteSpace(channel))
            {
                if (ParseChannel(channel) is not { } parsed)
                {
                    Console.Error.WriteLine($"Unknown --channel '{channel}'. Expected one of: {string.Join(", ", Enum.GetNames<ReleaseChannel>())}.");
                    return 1;
                }

                resolvedChannel = parsed;
            }
            else if (!string.IsNullOrWhiteSpace(propertyChannel))
            {
                if (ParseChannel(propertyChannel) is not { } parsed)
                {
                    Console.Error.WriteLine($"Unknown ReleaseChannel '{propertyChannel}'. Expected one of: {string.Join(", ", Enum.GetNames<ReleaseChannel>())}.");
                    return 1;
                }

                resolvedChannel = parsed;
            }

            if (resolvedChannel is not null)
                Console.WriteLine($"Release channel: {resolvedChannel}");

            // Determine which RIDs to build. If RuntimeIdentifiers is set
            // (semicolon-separated), build each one. Otherwise use the
            // single RuntimeIdentifier, or build portable and record "any".
            var ridListRaw = project.GetPropertyValue("RuntimeIdentifiers");
            var rids = !string.IsNullOrEmpty(ridListRaw)
                ? ridListRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : [];

            var portable = false;
            if (rids.Count == 0)
            {
                var single = project.GetPropertyValue("RuntimeIdentifier");
                if (string.IsNullOrEmpty(single))
                {
                    portable = true;
                    rids = ["any"];
                }
                else
                {
                    rids = [single];
                }
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

            var completed = new List<string>();

            foreach (var rid in rids)
            {
                Console.WriteLine($"Building for {rid}...");

                var ridProperties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase)
                {
                    ["EnableDynamicLoading"] = "true",
                };

                // "any" is not a RID; a portable build passes none at all.
                if (!portable)
                    ridProperties["RuntimeIdentifier"] = rid;

                var verbosity = ParseVerbosity(Environment.GetEnvironmentVariable("SHOKO_BUILD_VERBOSITY"))
                    ?? LoggerVerbosity.Minimal;

                // The assets file is per-RID, so a matrix restores per RID.
                if (restore && !RunRestore(projectFile, ridProperties, verbosity))
                {
                    Console.Error.WriteLine($"Restore failed for {rid}.");
                    continue;
                }

                // Fresh project instance per RID to pick up the correct properties
                var ridCollection = new ProjectCollection(ridProperties);
                var ridProject = new Project(projectFile, ridProperties, null, ridCollection);

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

                var buildParams = new BuildParameters
                {
                    Loggers = [new ConsoleLogger(verbosity)],
                };
                var requestData = new BuildRequestData(instance, [.. targets]);

                var result = BuildManager.DefaultBuildManager.Build(buildParams, requestData);
                ridCollection.UnloadAllProjects();
                ridCollection.Dispose();

                if (result.OverallResult != BuildResultCode.Success)
                {
                    Console.Error.WriteLine($"Build failed for {rid}.");
                    continue;
                }

                // ── Post-build: pack zip, checksum, update manifest ───
                var builtDll = FindBuiltDll(projectDir, instance);
                if (builtDll is null)
                {
                    Console.Error.WriteLine($"No plugin DLL found for {rid}.");
                    continue;
                }

                AssemblyMetadata metadata;
                try
                {
                    metadata = AssemblyInfoGenerator.ReadAssemblyMetadata(builtDll);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"Could not read metadata from the assembly built for {rid}: {ex.Message}");
                    continue;
                }

                var stampedRids = metadata.RuntimeIdentifiers
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var disagreeing = stampedRids
                    .Where(r => !string.Equals(r, rid, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (disagreeing.Count > 0)
                {
                    Console.Error.WriteLine(
                        $"Runtime identifier disagreement for {rid}: the built assembly is stamped " +
                        $"{string.Join(", ", stampedRids.Select(r => $"'{r}'"))}. Something in the build " +
                        $"set a RuntimeIdentifier other than the one being built. Not recording this archive.");
                    continue;
                }

                var versionStr = $"{metadata.Version.Major}.{metadata.Version.Minor}.{metadata.Version.Build}";
                if (metadata.Version.Revision > 0)
                    versionStr += $".{metadata.Version.Revision}";
                var abstractionStr = $"{metadata.AbstractionVersion.Major}.{metadata.AbstractionVersion.Minor}.{metadata.AbstractionVersion.Build}";

                // Resolve output path with template substitution
                var releaseTagOrVersion = !string.IsNullOrEmpty(releaseTag) ? releaseTag : $"v{versionStr}";
                var subst = new Dictionary<string, string>
                {
                    ["runtime"] = rid,
                    ["version"] = versionStr,
                    ["name"] = pluginName,
                    ["abstraction"] = abstractionStr,
                    ["tag"] = releaseTagOrVersion,
                };
                var zipPath = Path.GetFullPath(Substitute(outputZip, subst), projectDir);

                // Pack the build output into a zip
                var binDir = Path.GetDirectoryName(builtDll)!;
                var publishDir = Path.Combine(binDir, "publish");
                var sourceDir = Directory.Exists(publishDir) ? publishDir : binDir;

                var zipDir = Path.GetDirectoryName(zipPath);
                if (!string.IsNullOrEmpty(zipDir))
                    Directory.CreateDirectory(zipDir);

                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                var entryCount = PackDirectory(sourceDir, zipPath, Path.GetFileName(builtDll));

                // Compute SHA256 checksum
                string checksum;
                await using (var stream = File.OpenRead(zipPath))
                {
                    var hash = await SHA256.HashDataAsync(stream);
                    checksum = $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
                }

                Console.WriteLine($"Packed: {zipPath}");
                Console.WriteLine($"  {entryCount} entries, {new FileInfo(zipPath).Length:N0} bytes");
                Console.WriteLine($"  Checksum: {checksum}");

                var archiveUrl = Substitute(downloadUrl, subst);

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
                        checksum,
                        resolvedChannel,
                        releaseTagOrVersion);
                }

                completed.Add(rid);
            }

            // ── Save manifest once after all RIDs ─────────────────────
            if (manifest is not null && manifestFullPath is not null && completed.Count > 0)
            {
                if (pruneCount.HasValue)
                    ManifestManager.PruneReleases(manifest, pruneCount.Value, pruneMethod);

                ManifestManager.Save(manifest, manifestFullPath);
                Console.WriteLine($"Updated manifest: {manifestFullPath}");
            }

            var missing = rids.Where(r => !completed.Contains(r)).ToList();
            if (missing.Count > 0)
            {
                Console.Error.WriteLine(
                    $"Did not produce an archive for: {string.Join(", ", missing)}.");
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Build failed: {ex.Message}");
            return 1;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    ///   Pack <paramref name="sourceDir" /> into <paramref name="zipPath" />,
    ///   skipping the archive itself, a leftover <c>publish/</c>, and any
    ///   other build's output directory. Returns the number of entries.
    /// </summary>
    private static int PackDirectory(string sourceDir, string zipPath, string assemblyFileName)
    {
        var root = Path.GetFullPath(sourceDir);
        var archiveFullPath = Path.GetFullPath(zipPath);
        var entries = 0;

        using var stream = File.Create(zipPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var file in EnumeratePayload(root, root))
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            archive.CreateEntryFromFile(file, relative, CompressionLevel.Optimal);
            entries++;
        }

        return entries;

        IEnumerable<string> EnumeratePayload(string directory, string topLevel)
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (string.Equals(Path.GetFullPath(file), archiveFullPath, StringComparison.Ordinal))
                    continue;

                yield return file;
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(child);

                if (string.Equals(name, "publish", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(directory, topLevel, StringComparison.Ordinal))
                    continue;

                // A second copy of our assembly means another build's output.
                if (File.Exists(Path.Combine(child, assemblyFileName)))
                    continue;

                foreach (var file in EnumeratePayload(child, topLevel))
                    yield return file;
            }
        }
    }

    private static string Substitute(string template, Dictionary<string, string> values)
    {
        var result = template;
        foreach (var (key, value) in values)
            result = result.Replace($"{{{key}}}", value);
        return result;
    }

    private static string SanitizeRid(string rid)
        => rid.Replace('-', '_').Replace('.', '_');

    /// <summary>
    ///   Parse a release channel name, case-insensitively. Null when empty
    ///   or unrecognised; the caller distinguishes the two.
    /// </summary>
    private static ReleaseChannel? ParseChannel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.TryParse<ReleaseChannel>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    ///   Parse an MSBuild logger verbosity name, case-insensitively.
    /// </summary>
    private static LoggerVerbosity? ParseVerbosity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Enum.TryParse<LoggerVerbosity>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
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

    /// <summary>
    ///   Translate the arguments meant for <c>dotnet build</c> into MSBuild
    ///   global properties, adjusting <paramref name="targets" /> and
    ///   <paramref name="restore" /> for the arguments that are not
    ///   properties at all.
    /// </summary>
    private static Dictionary<string, string> CollectForwardArgs(
        string[] forwardArgs,
        List<string> targets,
        ref bool restore)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < forwardArgs.Length; i++)
        {
            var arg = forwardArgs[i];

            if (arg.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                arg.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                continue;

            if ((arg.StartsWith("-p:") || arg.StartsWith("/p:")) && arg.Contains('='))
            {
                var eq = arg.IndexOf('=');
                properties[arg[3..eq]] = arg[(eq + 1)..];
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
            { properties["Configuration"] = forwardArgs[++i]; continue; }

            if ((arg is "-f" or "--framework") && i + 1 < forwardArgs.Length)
            { properties["TargetFramework"] = forwardArgs[++i]; continue; }

            if ((arg is "-r" or "--runtime") && i + 1 < forwardArgs.Length)
            { properties["RuntimeIdentifier"] = forwardArgs[++i]; continue; }

            if (arg is "--no-restore")
            { restore = false; continue; }

            if (arg is "--no-build")
            { properties["BuildProject"] = "false"; continue; }

            if (arg is "--no-dependencies")
            { properties["BuildProjectReferences"] = "false"; continue; }
        }

        return properties;
    }

    /// <summary>
    ///   Run the <c>Restore</c> target for one set of properties, in its own
    ///   build submission and restore session the way MSBuild's
    ///   <c>-restore</c> switch does it.
    /// </summary>
    private static bool RunRestore(
        string projectFile,
        Dictionary<string, string> properties,
        LoggerVerbosity verbosity)
    {
        var restoreProperties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase)
        {
            ["MSBuildRestoreSessionId"] = Guid.NewGuid().ToString("D"),
            ["ExcludeRestorePackageImports"] = "true",
        };

        using var collection = new ProjectCollection(restoreProperties);
        var project = new Project(projectFile, restoreProperties, null, collection);
        var parameters = new BuildParameters
        {
            Loggers = [new ConsoleLogger(verbosity)],
        };

        var result = BuildManager.DefaultBuildManager.Build(
            parameters,
            new BuildRequestData(project.CreateProjectInstance(), ["Restore"]));

        collection.UnloadAllProjects();
        return result.OverallResult == BuildResultCode.Success;
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
        // TargetPath is this project's own output assembly; the heuristics
        // below only guess, and are kept as a fallback.
        var targetPath = instance.GetPropertyValue("TargetPath");
        if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
            return targetPath;

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
