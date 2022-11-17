using System;
using Mono.Unix;

namespace Shoko.Server.Utilities;

public static class LinuxFS
{
    private static bool CanRun()
    {
        return Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Unix;
    }

    public static void SetLinuxPermissions(string path, long uid, long gid, int mode)
    {
        if (!CanRun())
        {
            return;
        }

        if (uid < 0 || gid < 0)
        {
            var user = UnixUserInfo.GetRealUser();
            if (uid < 0)
                uid = user.UserId;
            if (gid < 0)
                gid = user.GroupId;
        }

        var file = new UnixFileInfo(path);
        file.SetOwner(uid, gid);
        if (mode > 0)
            file.FileAccessPermissions = (FileAccessPermissions)mode;
        // guarantee immediate flush
        file.Refresh();
    }
}
