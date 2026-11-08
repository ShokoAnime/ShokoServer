using Microsoft.Build.Framework;

namespace Shoko.BuildTools.Tasks;

/// <summary>
///   Writes the third-party notices file of a build: the licences of the
///   packages it ships, less those the host ships itself, and the assets and
///   manual licences the project lists. See <see cref="ThirdPartyNotices"/>.
/// </summary>
public class WriteThirdPartyNotices : Microsoft.Build.Utilities.Task
{
    /// <summary>
    ///   Where to write the notices file.
    /// </summary>
    [Required]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    ///   The project's folder, which relative text files are read from.
    /// </summary>
    public string? ProjectDirectory { get; set; }

    /// <summary>
    ///   The package files the build copies: <c>@(RuntimeCopyLocalItems)</c>,
    ///   <c>@(NativeCopyLocalItems)</c> and <c>@(ResourceCopyLocalItems)</c>,
    ///   with their <c>NuGetPackageId</c>, <c>NuGetPackageVersion</c> and
    ///   <c>PathInPackage</c>.
    /// </summary>
    public ITaskItem[]? PackageFiles { get; set; }

    /// <summary>
    ///   The project's <c>project.assets.json</c>, read for the packages the
    ///   host packages depend on.
    /// </summary>
    public string? ProjectAssetsFile { get; set; }

    /// <summary>
    ///   The packages the host ships, left out with everything they depend on.
    /// </summary>
    public string[]? HostPackages { get; set; }

    /// <summary>
    ///   The per-package cache folder.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    ///   <c>@(ThirdPartyNoticeOverride)</c>: a package ID, with <c>License</c>,
    ///   <c>TextFile</c>, <c>Copyright</c>, <c>Url</c> and an optional <c>Version</c>.
    /// </summary>
    public ITaskItem[]? Overrides { get; set; }

    /// <summary>
    ///   <c>@(ThirdPartyNotice)</c>: an asset no package carries, such as a
    ///   font, with <c>License</c>, <c>TextFile</c>, <c>Copyright</c>, <c>Url</c>
    ///   and <c>Version</c>.
    /// </summary>
    public ITaskItem[]? Assets { get; set; }

    /// <summary>
    ///   Whether a package with no known licence fails the build rather than
    ///   warning: on in CI.
    /// </summary>
    public bool TreatMissingAsError { get; set; }

    /// <summary>
    ///   The project's own name, version, licence and URL, heading its notices.
    /// </summary>
    public string? ProjectName { get; set; }

    public string? ProjectVersion { get; set; }

    public string? ProjectLicense { get; set; }

    public string? ProjectLicenseFile { get; set; }

    public string? ProjectCopyright { get; set; }

    public string? ProjectUrl { get; set; }

    public override bool Execute()
    {
        try
        {
            var shipped = ThirdPartyNotices.ShippedPackages((PackageFiles ?? []).Select(item => (
                item.GetMetadata("FullPath") is { Length: > 0 } fullPath ? fullPath : item.ItemSpec,
                (string?)item.GetMetadata("NuGetPackageId"),
                (string?)item.GetMetadata("NuGetPackageVersion"),
                (string?)item.GetMetadata("PathInPackage")
            )));
            var assetsJson = ProjectAssetsFile is { Length: > 0 } && File.Exists(ProjectAssetsFile) ? File.ReadAllText(ProjectAssetsFile) : null;
            var host = ThirdPartyNotices.HostClosure(assetsJson, HostPackages ?? []);

            var packages = new List<(ThirdPartyNotices.ShippedPackage, ThirdPartyNotices.PackageLicense)>();
            foreach (var package in shipped.Where(package => !host.Contains(package.ID)))
            {
                var license = ThirdPartyNotices.ReadPackageCached(CacheDirectory is { Length: > 0 } ? CacheDirectory : null, package)
                    ?? new(null, null, null, null, [], []);
                packages.Add((package, license));
            }

            var overrides = Manual(Overrides, "ThirdPartyNoticeOverride");
            var assets = Manual(Assets, "ThirdPartyNotice");
            if (overrides is null || assets is null)
                return false;

            var project = string.IsNullOrWhiteSpace(ProjectName) ? null : new ThirdPartyNotices.ManualLicense(
                ProjectName,
                NullIfEmpty(ProjectVersion),
                NullIfEmpty(ProjectLicense),
                ProjectLicenseFile is { Length: > 0 } && Resolve(ProjectLicenseFile) is var licenseFile && File.Exists(licenseFile) ? ThirdPartyNotices.ReadText(licenseFile) : null,
                NullIfEmpty(ProjectCopyright),
                NullIfEmpty(ProjectUrl)
            );

            var notices = ThirdPartyNotices.Build(project, packages, overrides, assets, out var missing);
            foreach (var package in missing)
            {
                const string message = "The package {0} has no licence a reader can see: no SPDX expression and no licence file, at most a URL. Add <ThirdPartyNoticeOverride Include=\"{1}\" License=\"<SPDX>\" TextFile=\"<file>\" /> to the project.";
                var id = package[..package.LastIndexOf('@')];
                if (TreatMissingAsError)
                    Log.LogError(null, "SHOKO0201", null, null, 0, 0, 0, 0, message, package, id);
                else
                    Log.LogWarning(null, "SHOKO0201", null, null, 0, 0, 0, 0, message, package, id);
            }

            ThirdPartyNotices.Write(OutputPath, notices);
            return !Log.HasLoggedErrors;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or System.Xml.XmlException)
        {
            Log.LogError("Could not write the third-party notices: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>
    ///   Reads manual licences from items, logging an error for one whose text
    ///   file is missing.
    /// </summary>
    private List<ThirdPartyNotices.ManualLicense>? Manual(ITaskItem[]? items, string itemName)
    {
        var result = new List<ThirdPartyNotices.ManualLicense>();
        var ok = true;
        foreach (var item in items ?? [])
        {
            string? text = null;
            if (item.GetMetadata("TextFile") is { Length: > 0 } textFile)
            {
                var path = Resolve(textFile);
                text = ThirdPartyNotices.ReadText(path);
                if (text is null)
                {
                    Log.LogError("The TextFile '{0}' of {1} '{2}' is missing or empty.", textFile, itemName, item.ItemSpec);
                    ok = false;
                    continue;
                }
            }

            result.Add(new(
                item.ItemSpec,
                NullIfEmpty(item.GetMetadata("Version")),
                NullIfEmpty(item.GetMetadata("License")),
                text,
                NullIfEmpty(item.GetMetadata("Copyright")),
                NullIfEmpty(item.GetMetadata("Url"))
            ));
        }

        return ok ? result : null;
    }

    private string Resolve(string path)
        => ProjectDirectory is { Length: > 0 } ? Path.GetFullPath(path, ProjectDirectory) : Path.GetFullPath(path);

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
