using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Shoko.Abstractions.Core.Services;

#nullable enable
namespace Shoko.Server.API.FileProviders;

public class WebUiFileProvider : PhysicalFileProvider, IFileProvider
{
    private readonly ISystemUpdateService _webuiUpdateService;

    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly string _prefix;

    private IFileInfo? _indexFile;

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

    private void OnUpdateInstalled(object? sender, EventArgs e) => _indexFile = null;

    public new IDirectoryContents GetDirectoryContents(string subpath)
    {
        return base.GetDirectoryContents(subpath);
    }

    public new IFileInfo GetFileInfo(string subpath)
    {
        // Anti-lockout for APIv2+ requests.
        if (_prefix is "" && (subpath is "/api" or "/signalr" or "/plex" || subpath.StartsWith("/api/") || subpath.StartsWith("/signalr/") || subpath.StartsWith("/plex/")))
            return new NotFoundFileInfo(subpath);

        if (subpath is "/manifest.webmanifest" or "/manifest.json")
            return GetWebManifest();

        var fileInfo = base.GetFileInfo(subpath);
        if (fileInfo is NotFoundFileInfo or { Exists: false } || subpath is "/" or "/index.html")
            return GetIndexFileInfo();

        return fileInfo;
    }

    private IFileInfo GetIndexFileInfo()
    {
        if (_indexFile is not null)
            return _indexFile;

        lock (this)
        {
            if (_indexFile is not null)
                return _indexFile;

            var indexFile = base.GetFileInfo("index.html");
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

            _indexFile = indexFile ?? new NotFoundFileInfo("index.html");
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

        public MemoryFileInfo(string name, byte[] fileContents)
        {
            Name = name;
            _fileContents = fileContents;
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
        ///   The current time.
        /// </summary>
        public DateTimeOffset LastModified { get; } = DateTimeOffset.Now;

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
