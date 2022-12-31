using Microsoft.Extensions.FileProviders;

namespace Shoko.Server.API.WebUI;

public class WebUiFileProvider : PhysicalFileProvider, IFileProvider
{
    public WebUiFileProvider(string root) : base(root)
    {
    }

    public new IDirectoryContents GetDirectoryContents(string subpath)
    {
        return base.GetDirectoryContents(subpath);
    }

    public new IFileInfo GetFileInfo(string subpath)
    {
        var fileInfo = base.GetFileInfo(subpath);
        if (fileInfo is NotFoundFileInfo || !fileInfo.Exists)
        {
            return base.GetFileInfo("index.html");
        }

        return fileInfo;
    }
}
