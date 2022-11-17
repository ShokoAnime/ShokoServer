using System;
using Mono.Unix;

namespace Shoko.Server.Utilities;

public static class LinuxFS
{
    private static UnixUserInfo RealUser = UnixUserInfo.GetRealUser();
    
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
            if (uid < 0)
                uid = RealUser.UserId;
            if (gid < 0)
                gid = RealUser.GroupId;
        }

        var file = new UnixFileInfo(path);
        var changed = false;
        if (file.OwnerUserId != uid || file.OwnerGroupId != gid)
        {
            file.SetOwner(uid, gid);
            changed = true;
        }
        if (mode > 0)
        {
            file.FileAccessPermissions = (FileAccessPermissions)mode;
            changed = true;
        }
        if (changed)
        {
            // guarantee immediate flush
            file.Refresh();
        }
    }
}
