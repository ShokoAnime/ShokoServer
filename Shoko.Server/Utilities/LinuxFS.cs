using System;
using Mono.Unix;

namespace Shoko.Server.Utilities;

public static class LinuxFS
{
    private static bool CanRun()
    {
        return Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Unix;
    }

    public static void SetLinuxPermissions(string path, int uid, int gid, int mode)
    {
        if (!CanRun())
        {
            return;
        }

        var file = new UnixFileInfo(path);
        file.SetOwner(uid, gid);
        file.FileAccessPermissions = (FileAccessPermissions)mode;
        // guarantee immediate flush
        file.Refresh();
    }
}
