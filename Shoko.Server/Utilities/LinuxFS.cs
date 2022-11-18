using System;
using System.Threading;
using Mono.Unix;

namespace Shoko.Server.Utilities;

public static class LinuxFS
{
    private static UnixUserInfo RealUser;
    
    private static bool CanRun()
    {
        return Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Unix;
    }

    public static void SetLinuxPermissions(string path, long uid, long gid, int mode)
    {
        if (!CanRun())
            return;

        RealUser ??= UnixUserInfo.GetRealUser();

        if (uid <= 0 && RealUser.UserId != 0)
            uid = RealUser.UserId;

        if (gid <= 0 && RealUser.GroupId != 0)
            gid = RealUser.GroupId;

        var file = new UnixFileInfo(path);
        var changed = false;
        if (file.OwnerUserId != uid || file.OwnerGroupId != gid)
        {
            file.SetOwner(uid, gid);
            changed = true;
        }
        if (mode > 0 && file.FileAccessPermissions != (FileAccessPermissions)mode)
        {
            file.FileAccessPermissions = (FileAccessPermissions)mode;
            changed = true;
        }
        if (changed)
        {
            // guarantee immediate flush
            file.Refresh();
            
            // spinwait to ensure the status is updated
            var time = 2000;
            var interval = 50;
            var limit = time / interval;
            var count = 0;
            while (file.OwnerUserId != uid || file.OwnerGroupId != gid || file.FileAccessPermissions != (FileAccessPermissions)mode && count < limit)
            {
                Thread.Sleep(interval);
                count++;
                file.Refresh();
            }
        }
    }
}
