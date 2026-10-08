using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Shoko.Abstractions.Core.Services;

namespace Shoko.Server.API.FileProviders;

public class WebUiFileProvider : PhysicalFileProvider, IFileProvider
{
    private readonly ISystemUpdateService _webuiUpdateService;

    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly string _prefix;

    private IFileInfo? _indexFile;

    private readonly ConcurrentDictionary<string, IFileInfo> _stylesheets = new(StringComparer.Ordinal);

    /// <summary>
    ///   Extensions of the files a WebUI build serves; a missing path with one
    ///   of them is a missing asset, not a client route.
    /// </summary>
    private static readonly HashSet<string> _assetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".mjs", ".css", ".map", ".woff", ".woff2", ".ttf", ".otf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".svg", ".ico",
        ".json", ".webmanifest", ".txt", ".wasm",
    };

    public WebUiFileProvider(ISystemUpdateService webuiUpdateService, IHttpContextAccessor httpContextAccessor, string prefix, string root) : base(root)
    {
        _webuiUpdateService = webuiUpdateService;
        _httpContextAccessor = httpContextAccessor;
        _prefix = prefix;

        _webuiUpdateService.WebComponentUpdated += OnUpdateInstalled;
    }

    ~WebUiFileProvider()
    {
        _webuiUpdateService.WebComponentUpdated -= OnUpdateInstalled;
    }

    private void OnUpdateInstalled(object? sender, EventArgs e)
    {
        _indexFile = null;
        _stylesheets.Clear();
    }

    public new IDirectoryContents GetDirectoryContents(string subpath)
    {
        return base.GetDirectoryContents(subpath);
    }

    public new IFileInfo GetFileInfo(string subpath)
    {
        // Anti-lockout for APIv2+ & plugin requests.
        if (_prefix is "" && (subpath is "/api" or "/signalr" or "/plex" or "/plugin" || subpath.StartsWith("/api/") || subpath.StartsWith("/signalr/") || subpath.StartsWith("/plex/") || subpath.StartsWith("/plugin/")))
            return new NotFoundFileInfo(subpath);

        if (subpath is "/manifest.webmanifest" or "/manifest.json")
            return GetWebManifest();

        if (subpath is "/" or "/index.html")
            return GetIndexFileInfo();

        var fileInfo = base.GetFileInfo(subpath);
        if (fileInfo is NotFoundFileInfo or { Exists: false })
            return IsAssetPath(subpath) ? new NotFoundFileInfo(subpath) : GetIndexFileInfo();

        if (subpath.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            return GetStylesheet(subpath, fileInfo);

        return fileInfo;
    }

    /// <summary>
    ///   Checks if a missing path is a missing asset rather than a client route.
    /// </summary>
    /// <param name="subpath">The requested path.</param>
    /// <returns><c>true</c> if the path is under <c>/assets/</c> or ends in a known asset extension.</returns>
    private static bool IsAssetPath(string subpath)
        => subpath.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) || _assetExtensions.Contains(Path.GetExtension(subpath));

    /// <summary>
    ///   Gets a stylesheet with the built-in <c>/webui</c> prefix in its URLs
    ///   replaced by the configured prefix.
    /// </summary>
    /// <param name="subpath">The requested path.</param>
    /// <param name="fileInfo">The physical stylesheet.</param>
    /// <returns>The physical file for the built-in prefix, otherwise a cached rewritten copy.</returns>
    private IFileInfo GetStylesheet(string subpath, IFileInfo fileInfo)
    {
        if (_prefix is "/webui" || fileInfo is not { PhysicalPath.Length: > 0 })
            return fileInfo;

        if (_stylesheets.TryGetValue(subpath, out var cached) && fileInfo.LastModified <= cached.LastModified)
            return cached;

        var bytes = Encoding.UTF8.GetBytes(
            File.ReadAllText(fileInfo.PhysicalPath)
                .Replace("url(/webui/", $"url({_prefix}/")
                .Replace("url(\"/webui/", $"url(\"{_prefix}/")
                .Replace("url('/webui/", $"url('{_prefix}/")
        );
        var stylesheet = new MemoryFileInfo(fileInfo.Name, bytes, fileInfo.LastModified);
        _stylesheets[subpath] = stylesheet;
        return stylesheet;
    }

    private IFileInfo GetIndexFileInfo()
    {
        var indexFile = base.GetFileInfo("index.html") ?? new NotFoundFileInfo("index.html");
        if (_indexFile is not null && (indexFile is not { Exists: true, PhysicalPath.Length: > 0, Length: > 0 } || indexFile.LastModified < _indexFile.LastModified))
            return _indexFile;

        lock (_webuiUpdateService)
        {
            indexFile = base.GetFileInfo("index.html") ?? new NotFoundFileInfo("index.html");
            if (_indexFile is not null && (indexFile is not { Exists: true, PhysicalPath.Length: > 0, Length: > 0 } || indexFile.LastModified < _indexFile.LastModified))
                return _indexFile;

            if (indexFile is { Exists: true, PhysicalPath.Length: > 0, Length: > 0 })
            {
                var bytes = Encoding.UTF8.GetBytes(
                    File.ReadAllText(indexFile.PhysicalPath)
                        .Replace("WEBUI_PREFIX='/webui';", $"WEBUI_PREFIX='{_prefix}';")
                        .Replace("href=\"/webui/", $"href=\"{_prefix}/")
                        .Replace("src=\"/webui/", $"src=\"{_prefix}/")
                );
                indexFile = new MemoryFileInfo("index.html", bytes);
            }

            _indexFile = indexFile;
            return _indexFile;
        }
    }

    private IFileInfo GetWebManifest()
    {
        var manifest = base.GetFileInfo("/manifest.webmanifest");
        if (manifest is NotFoundFileInfo or { Exists: false })
            manifest = base.GetFileInfo("/manifest.json");
        if (manifest is { Exists: true, PhysicalPath.Length: > 0, Length: > 0 })
        {
            var prefix = _prefix;
            if (prefix is "")
                prefix = "/";
            var request = _httpContextAccessor.HttpContext!.Request;
            var baseUrl = new UriBuilder(
                request.Scheme,
                request.Host.Host,
                request.Host.Port ?? (request.Scheme == "https" ? 443 : 80),
                request.PathBase + prefix,
                null
            )
                .ToString();
            var baseUrlWithTrailingSlash = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
            var bytes = Encoding.UTF8.GetBytes(
                File.ReadAllText(manifest.PhysicalPath)
                    .Replace("\": \"/webui\"", $"\": \"{baseUrl}\"")
                    .Replace("\": \"/webui/", $"\": \"{baseUrlWithTrailingSlash}")
                );
            return new MemoryFileInfo(manifest.Name, bytes);
        }

        return new NotFoundFileInfo("/manifest.webmanifest");
    }

    /// <summary>
    ///   Represents a file stored in memory.
    /// </summary>
    public class MemoryFileInfo : IFileInfo
    {
        /// <summary>
        ///   The file contents.
        /// </summary>
        private readonly byte[] _fileContents;

        public MemoryFileInfo(string name, byte[] fileContents, DateTimeOffset? lastModified = null)
        {
            Name = name;
            _fileContents = fileContents;
            LastModified = lastModified ?? DateTimeOffset.Now;
        }

        /// <summary>
        ///   Always true.
        /// </summary>
        public bool Exists { get; } = true;

        /// <summary>
        ///   Always false.
        /// </summary>
        public bool IsDirectory { get; } = false;

        /// <summary>
        ///   The time given at creation, or the creation time.
        /// </summary>
        public DateTimeOffset LastModified { get; }

        /// <summary>
        ///   The length of the file content in bytes.
        /// </summary>
        public long Length { get => _fileContents.Length; }

        /// <summary>
        ///   The name of the file.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///   Always null.
        /// </summary>
        public string? PhysicalPath { get; } = null;

        public Stream CreateReadStream() => new MemoryStream(_fileContents);
    }
}
