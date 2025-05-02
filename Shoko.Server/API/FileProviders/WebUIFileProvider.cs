using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Shoko.Server.Services;

#nullable enable
namespace Shoko.Server.API.FileProviders;

public class WebUiFileProvider : PhysicalFileProvider, IFileProvider
{
    private readonly WebUIUpdateService _webuiUpdateService;

    private readonly string _prefix;

    private IFileInfo? _indexFile;

    public WebUiFileProvider(WebUIUpdateService webuiUpdateService, string prefix, string root) : base(root)
    {
        _webuiUpdateService = webuiUpdateService;
        _prefix = prefix;

        _webuiUpdateService.UpdateInstalled += OnUpdateInstalled;
    }

    ~WebUiFileProvider()
    {
        _webuiUpdateService.UpdateInstalled -= OnUpdateInstalled;
    }

    private void OnUpdateInstalled(object? sender, EventArgs e) => _indexFile = null;

    public new IDirectoryContents GetDirectoryContents(string subpath)
    {
        return base.GetDirectoryContents(subpath);
    }

    public new IFileInfo GetFileInfo(string subpath)
    {
        // Anti-lockout for APIv2+ requests.
        if (_prefix is "" && (subpath is "/plex" or "/plex.json" || subpath.StartsWith("/api")))
            return new NotFoundFileInfo(subpath);

        var fileInfo = base.GetFileInfo(subpath);
        if (fileInfo is NotFoundFileInfo || !fileInfo.Exists || subpath is "/" or "/index.html")
        {
            // Asset remapping for the default built webui when mounted at the root.
            if (_prefix is "" && subpath.StartsWith("/webui/") && base.GetFileInfo(subpath[6..]) is { Exists: true, Length: > 0 } webuiFile)
                return webuiFile;

            return GetIndexFileInfo();
        }

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
                var bytes = Encoding.UTF8.GetBytes(File.ReadAllText(indexFile.PhysicalPath).Replace("WEBUI_PREFIX='/webui';", $"WEBUI_PREFIX='/{_prefix}';"));
                indexFile = new MemoryFileInfo("index.html", bytes);
            }

            _indexFile = indexFile ?? new NotFoundFileInfo("index.html");
            return _indexFile;
        }
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
