using Microsoft.Extensions.FileProviders;

namespace Shoko.Server.API.FileProviders;

public class WebUiFileProvider : PhysicalFileProvider, IFileProvider
{
    private readonly string _prefix;

    public WebUiFileProvider(string prefix, string root) : base(root)
    {
        _prefix = prefix;
    }

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
        if (fileInfo is NotFoundFileInfo || !fileInfo.Exists)
            return base.GetFileInfo("index.html");

        return fileInfo;
    }
}
