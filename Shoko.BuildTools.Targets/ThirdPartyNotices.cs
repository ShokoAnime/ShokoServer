using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Shoko.BuildTools.Tasks;

/// <summary>
///   Collects the licences of the third-party packages a build ships into the
///   notices file the server reads beside a plugin: <c>third-party-notices.json.br</c>.
///   The format is described in the README of <c>Shoko.BuildTools</c>.
/// </summary>
public static class ThirdPartyNotices
{
    /// <summary>
    ///   The notices file's name.
    /// </summary>
    public const string FileName = "third-party-notices.json.br";

    /// <summary>
    ///   The format version written.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    ///   The format version of the per-package cache files. A cache file of
    ///   another version is read again from the package.
    /// </summary>
    internal const int CacheVersion = 1;

    /// <summary>
    ///   The names a package's licence files start with.
    /// </summary>
    private static readonly string[] _licenceFilePrefixes = ["LICENSE", "LICENCE", "COPYING"];

    /// <summary>
    ///   The names the other notice files a package carries start with.
    /// </summary>
    private static readonly string[] _noticeFilePrefixes = ["NOTICE", "THIRD-PARTY", "THIRDPARTY", "THIRD_PARTY", "AUTHORS"];

    /// <summary>
    ///   The extensions a notice file may have, so a <c>LICENSE.dll</c> or an
    ///   image is never read as text.
    /// </summary>
    private static readonly string[] _textExtensions = ["", ".txt", ".md", ".markdown", ".rst", ".text"];

    /// <summary>
    ///   The largest notice file read, in bytes.
    /// </summary>
    private const long MaxTextFileSize = 1024 * 1024;

    /// <summary>
    ///   A package the build ships.
    /// </summary>
    /// <param name="ID">The package ID.</param>
    /// <param name="Version">The package version.</param>
    /// <param name="Root">The package's folder in the package cache, or <c>null</c> when unknown.</param>
    public sealed record ShippedPackage(string ID, string Version, string? Root);

    /// <summary>
    ///   What a package says about its licence.
    /// </summary>
    /// <param name="License">The SPDX expression, as written, or <c>null</c> when the package gives none.</param>
    /// <param name="LicenseUrl">The licence URL the package gives instead, if any.</param>
    /// <param name="Copyright">The copyright line, if any.</param>
    /// <param name="Url">The project URL, if any.</param>
    /// <param name="LicenseTexts">The licence texts the package carries.</param>
    /// <param name="NoticeTexts">The other notice texts it carries, NOTICE and AUTHORS files among them.</param>
    public sealed record PackageLicense(
        string? License,
        string? LicenseUrl,
        string? Copyright,
        string? Url,
        IReadOnlyList<string> LicenseTexts,
        IReadOnlyList<string> NoticeTexts
    )
    {
        /// <summary>
        ///   Whether the licence is known: an SPDX expression or a licence text.
        ///   A licence URL alone does not count.
        /// </summary>
        public bool IsKnown => !string.IsNullOrWhiteSpace(License) || LicenseTexts.Count > 0;
    }

    /// <summary>
    ///   A manual licence for a package, or for an asset no package carries.
    /// </summary>
    /// <param name="ID">The package ID, or the asset's name.</param>
    /// <param name="Version">The version it applies to, or <c>null</c> for every version.</param>
    /// <param name="License">The SPDX expression, if given.</param>
    /// <param name="Text">The licence text, if given.</param>
    /// <param name="Copyright">The copyright line, if given.</param>
    /// <param name="Url">The URL, if given.</param>
    public sealed record ManualLicense(string ID, string? Version, string? License, string? Text, string? Copyright, string? Url);

    /// <summary>
    ///   One entry of the notices file.
    /// </summary>
    /// <param name="ID">The package ID or asset name.</param>
    /// <param name="Version">The version, if known.</param>
    /// <param name="Source"><c>nuget</c>, <c>npm</c> or <c>asset</c>; <c>project</c> for the project's own entry.</param>
    /// <param name="License">The SPDX expression, as written.</param>
    /// <param name="Copyright">The copyright line.</param>
    /// <param name="Url">The URL.</param>
    /// <param name="TextKeys">The keys of its texts in the file's text table, licence texts first.</param>
    public sealed record Entry(string ID, string? Version, string Source, string? License, string? Copyright, string? Url, IReadOnlyList<string> TextKeys);

    /// <summary>
    ///   The whole notices file.
    /// </summary>
    /// <param name="Project">The project's own entry, if it has a licence.</param>
    /// <param name="Packages">The packages and assets, sorted.</param>
    /// <param name="Texts">The texts by key.</param>
    public sealed record Notices(Entry? Project, IReadOnlyList<Entry> Packages, IReadOnlyDictionary<string, string> Texts);

    #region Collecting

    /// <summary>
    ///   Works out the packages a build ships from its copy-local items: one per
    ///   package ID and version, its folder taken from the item's path less its
    ///   path in the package.
    /// </summary>
    /// <param name="items">The items: their full path, <c>NuGetPackageId</c>, <c>NuGetPackageVersion</c> and <c>PathInPackage</c>.</param>
    /// <returns>The packages, each once.</returns>
    public static IReadOnlyList<ShippedPackage> ShippedPackages(IEnumerable<(string Path, string? PackageID, string? PackageVersion, string? PathInPackage)> items)
    {
        var packages = new Dictionary<(string, string), ShippedPackage>();
        foreach (var (path, packageID, packageVersion, pathInPackage) in items)
        {
            if (string.IsNullOrWhiteSpace(packageID) || string.IsNullOrWhiteSpace(packageVersion))
                continue;

            var key = (packageID.ToLowerInvariant(), packageVersion.ToLowerInvariant());
            if (packages.TryGetValue(key, out var known) && known.Root is not null)
                continue;

            packages[key] = new(packageID, packageVersion, RootOf(path, pathInPackage));
        }

        return [.. packages.Values];
    }

    /// <summary>
    ///   A package's folder, from a file in it and the file's path in the package.
    /// </summary>
    private static string? RootOf(string path, string? pathInPackage)
    {
        if (string.IsNullOrWhiteSpace(pathInPackage))
            return null;

        var full = Path.GetFullPath(path).Replace('\\', '/');
        var inPackage = pathInPackage.Replace('\\', '/').TrimStart('/');
        return full.EndsWith("/" + inPackage, StringComparison.OrdinalIgnoreCase)
            ? full[..^(inPackage.Length + 1)]
            : null;
    }

    /// <summary>
    ///   The packages the host already ships, so a plugin does not list them:
    ///   the given packages and everything they depend on, as the project's
    ///   assets file records it.
    /// </summary>
    /// <param name="assetsJson">The contents of <c>project.assets.json</c>.</param>
    /// <param name="hostPackages">The packages the host provides, such as <c>Shoko.Abstractions</c>.</param>
    /// <returns>The IDs, compared ignoring case.</returns>
    public static IReadOnlySet<string> HostClosure(string? assetsJson, IEnumerable<string> hostPackages)
    {
        var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = hostPackages.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList();
        if (roots.Count is 0 || string.IsNullOrWhiteSpace(assetsJson))
            return closure;

        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        using (var document = JsonDocument.Parse(assetsJson))
        {
            if (document.RootElement.TryGetProperty("targets", out var targets) && targets.ValueKind is JsonValueKind.Object)
            {
                foreach (var target in targets.EnumerateObject())
                {
                    foreach (var library in target.Value.EnumerateObject())
                    {
                        var id = library.Name.Split('/')[0];
                        if (!dependencies.TryGetValue(id, out var set))
                            dependencies[id] = set = new(StringComparer.OrdinalIgnoreCase);
                        if (library.Value.TryGetProperty("dependencies", out var deps) && deps.ValueKind is JsonValueKind.Object)
                            foreach (var dependency in deps.EnumerateObject())
                                set.Add(dependency.Name);
                    }
                }
            }
        }

        var queue = new Queue<string>(roots);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!closure.Add(id))
                continue;
            if (dependencies.TryGetValue(id, out var deps))
                foreach (var dependency in deps)
                    queue.Enqueue(dependency);
        }

        return closure;
    }

    /// <summary>
    ///   Reads what a package says about its licence: its nuspec and the
    ///   licence and notice files in its folder.
    /// </summary>
    /// <param name="root">The package's folder.</param>
    /// <param name="packageID">The package ID, which names its nuspec.</param>
    /// <returns>The licence.</returns>
    public static PackageLicense ReadPackage(string root, string packageID)
    {
        string? license = null, licenseUrl = null, copyright = null, url = null;
        var licenseTexts = new List<string>();
        var noticeTexts = new List<string>();
        var read = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var nuspec = Path.Combine(root, packageID.ToLowerInvariant() + ".nuspec");
        if (!File.Exists(nuspec))
            nuspec = Directory.Exists(root) ? Directory.EnumerateFiles(root, "*.nuspec").FirstOrDefault() ?? nuspec : nuspec;
        if (File.Exists(nuspec))
        {
            var metadata = XDocument.Load(nuspec).Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "metadata");
            string? Value(string name) => metadata?.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value.Trim() is { Length: > 0 } value ? value : null;

            copyright = Value("copyright");
            url = Value("projectUrl");
            licenseUrl = Value("licenseUrl");
            var licenseElement = metadata?.Elements().FirstOrDefault(element => element.Name.LocalName == "license");
            var type = licenseElement?.Attribute("type")?.Value;
            if (licenseElement is not null && string.Equals(type, "expression", StringComparison.OrdinalIgnoreCase))
                license = licenseElement.Value.Trim() is { Length: > 0 } expression ? expression : null;
            else if (licenseElement is not null && string.Equals(type, "file", StringComparison.OrdinalIgnoreCase))
            {
                var file = Path.GetFullPath(Path.Combine(root, licenseElement.Value.Trim()));
                if (IsInside(root, file) && ReadText(file) is { } text)
                {
                    licenseTexts.Add(text);
                    read.Add(file);
                }
            }

            // The URL nuget.org puts in place of an expression says nothing a reader needs.
            if (licenseUrl is not null && licenseUrl.StartsWith("https://licenses.nuget.org/", StringComparison.OrdinalIgnoreCase))
                licenseUrl = null;
        }

        if (Directory.Exists(root))
        {
            foreach (var file in Directory.EnumerateFiles(root).Order(StringComparer.Ordinal))
            {
                if (read.Contains(Path.GetFullPath(file)) || !IsTextFile(file))
                    continue;

                var name = Path.GetFileName(file);
                var target = _licenceFilePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ? licenseTexts
                    : _noticeFilePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ? noticeTexts
                    : null;
                if (target is not null && ReadText(file) is { } text && !target.Contains(text))
                    target.Add(text);
            }
        }

        return new(license, licenseUrl, copyright, url, licenseTexts, noticeTexts);
    }

    private static bool IsInside(string root, string file)
        => file.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static bool IsTextFile(string file)
        => _textExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///   Reads a text file, its line endings made <c>\n</c> and trailing white
    ///   space trimmed, so the same text always gets the same key.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The text, or <c>null</c> when it is missing, empty or too large.</returns>
    internal static string? ReadText(string file)
    {
        var info = new FileInfo(file);
        if (!info.Exists || info.Length is 0 or > MaxTextFileSize)
            return null;

        return Normalize(File.ReadAllText(file));
    }

    private static string? Normalize(string? text)
        => text?.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd() is { Length: > 0 } normalized ? normalized : null;

    /// <summary>
    ///   The key a text is stored under: the first 16 bytes of its SHA-256, as
    ///   lowercase hex.
    /// </summary>
    /// <param name="text">The text, normalized.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(string text)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)).AsSpan(0, 16));

    #endregion

    #region Building

    /// <summary>
    ///   Puts the notices together: each package's licence, a manual licence
    ///   filling in or replacing what the package says, and the assets no
    ///   package carries.
    /// </summary>
    /// <param name="project">The project's own licence, or <c>null</c> when it gives none.</param>
    /// <param name="packages">The packages shipped, with their licences.</param>
    /// <param name="overrides">The manual licences of packages.</param>
    /// <param name="assets">The assets no package carries.</param>
    /// <param name="missing">The packages whose licence is still not known, as <c>id@version</c>.</param>
    /// <returns>The notices.</returns>
    public static Notices Build(
        ManualLicense? project,
        IEnumerable<(ShippedPackage Package, PackageLicense License)> packages,
        IReadOnlyList<ManualLicense> overrides,
        IEnumerable<ManualLicense> assets,
        out IReadOnlyList<string> missing
    )
    {
        var texts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var stillMissing = new List<string>();

        List<string> Keys(IEnumerable<string?> values)
        {
            var keys = new List<string>();
            foreach (var value in values)
            {
                if (Normalize(value) is not { } text)
                    continue;
                var key = KeyOf(text);
                texts[key] = text;
                if (!keys.Contains(key))
                    keys.Add(key);
            }
            return keys;
        }

        var entries = new List<Entry>();
        foreach (var (package, license) in packages)
        {
            var manual = overrides.FirstOrDefault(entry => string.Equals(entry.ID, package.ID, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Version, package.Version, StringComparison.OrdinalIgnoreCase))
                ?? overrides.FirstOrDefault(entry => string.Equals(entry.ID, package.ID, StringComparison.OrdinalIgnoreCase) && entry.Version is null);
            var expression = manual?.License ?? license.License;
            IReadOnlyList<string> licenseTexts = manual?.Text is { } manualText ? [manualText] : license.LicenseTexts;
            if (string.IsNullOrWhiteSpace(expression) && !licenseTexts.Any(text => Normalize(text) is not null))
                stillMissing.Add($"{package.ID}@{package.Version}");

            entries.Add(new(
                package.ID,
                package.Version,
                "nuget",
                expression,
                manual?.Copyright ?? license.Copyright,
                manual?.Url ?? license.Url ?? license.LicenseUrl,
                Keys(licenseTexts.Concat(license.NoticeTexts))
            ));
        }

        foreach (var asset in assets)
            entries.Add(new(asset.ID, asset.Version, "asset", asset.License, asset.Copyright, asset.Url, Keys([asset.Text])));

        var projectEntry = project is null || (string.IsNullOrWhiteSpace(project.License) && Normalize(project.Text) is null)
            ? null
            : new Entry(project.ID, project.Version, "project", project.License, project.Copyright, project.Url, Keys([project.Text]));

        missing = stillMissing;
        return new(
            projectEntry,
            [.. entries.OrderBy(entry => entry.ID, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.ID, StringComparer.Ordinal).ThenBy(entry => entry.Version, StringComparer.Ordinal).ThenBy(entry => entry.Source, StringComparer.Ordinal)],
            texts
        );
    }

    #endregion

    #region Writing

    /// <summary>
    ///   Writes the notices as JSON: compact, members and entries in a fixed
    ///   order and no timestamps, so the same input always gives the same bytes.
    /// </summary>
    /// <param name="notices">The notices.</param>
    /// <returns>The UTF-8 JSON.</returns>
    public static byte[] ToJson(Notices notices)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", FormatVersion);
            if (notices.Project is { } project)
            {
                writer.WritePropertyName("project");
                WriteEntry(writer, project);
            }

            writer.WriteStartArray("packages");
            foreach (var entry in notices.Packages)
                WriteEntry(writer, entry);
            writer.WriteEndArray();

            writer.WriteStartObject("texts");
            foreach (var (key, text) in notices.Texts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                writer.WriteString(key, text);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteEntry(Utf8JsonWriter writer, Entry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("id", entry.ID);
        WriteOptional(writer, "version", entry.Version);
        writer.WriteString("source", entry.Source);
        WriteOptional(writer, "license", entry.License);
        WriteOptional(writer, "copyright", entry.Copyright);
        WriteOptional(writer, "url", entry.Url);
        writer.WriteStartArray("textKeys");
        foreach (var key in entry.TextKeys)
            writer.WriteStringValue(key);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            writer.WriteString(name, value);
    }

    /// <summary>
    ///   Writes the notices file: the JSON, Brotli compressed at a fixed level,
    ///   so the same notices always give the same bytes.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="notices">The notices.</param>
    public static void Write(string path, Notices notices)
    {
        using var compressed = new MemoryStream();
        using (var brotli = new BrotliStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            brotli.Write(ToJson(notices));

        var bytes = compressed.ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, bytes);
    }

    #endregion

    #region Cache

    /// <summary>
    ///   Reads a package's licence through a cache of one file per exact ID and
    ///   version, since a package in the cache never changes.
    /// </summary>
    /// <param name="cacheDirectory">The cache folder, or <c>null</c> to read the package every time.</param>
    /// <param name="package">The package.</param>
    /// <returns>The licence, or <c>null</c> when the package's folder is unknown and nothing is cached.</returns>
    public static PackageLicense? ReadPackageCached(string? cacheDirectory, ShippedPackage package)
    {
        var cacheFile = cacheDirectory is null ? null : Path.Combine(cacheDirectory, $"{package.ID.ToLowerInvariant()}@{package.Version.ToLowerInvariant()}.json");
        if (cacheFile is not null && File.Exists(cacheFile))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<CachedLicense>(File.ReadAllText(cacheFile));
                if (cached is { Version: CacheVersion })
                    return new(cached.License, cached.LicenseUrl, cached.Copyright, cached.Url, cached.LicenseTexts ?? [], cached.NoticeTexts ?? []);
            }
            catch (JsonException)
            {
                // Read the package again below.
            }
        }

        if (package.Root is null || !Directory.Exists(package.Root))
            return null;

        var license = ReadPackage(package.Root, package.ID);
        if (cacheFile is not null)
        {
            Directory.CreateDirectory(cacheDirectory!);
            File.WriteAllText(cacheFile, JsonSerializer.Serialize(new CachedLicense(CacheVersion, license.License, license.LicenseUrl, license.Copyright, license.Url, [.. license.LicenseTexts], [.. license.NoticeTexts])));
        }

        return license;
    }

    private sealed record CachedLicense(int Version, string? License, string? LicenseUrl, string? Copyright, string? Url, List<string>? LicenseTexts, List<string>? NoticeTexts);

    #endregion
}
