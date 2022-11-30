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

        // if uid is not valid (< 0) and we are not root (RealUser.UserId != 0), then set it to the current user
        // if uid = 0 (trying to set as root), and we are not root (RealUser.UserId != 0), then set it to the current user
        // if uid = 0 and we are root (RealUser.UserId = 0), then nothing changes, and it can use those values
        if (uid <= 0 && RealUser.UserId != 0)
            uid = RealUser.UserId;

        if (gid <= 0 && RealUser.GroupId != 0)
            gid = RealUser.GroupId;

        var file = new UnixFileInfo(path);
        // new user ID is valid (>= 0) and has changed (or same for group ID)
        var shouldChangeOwner = uid >= 0 && uid != file.OwnerUserId || gid >= 0 && file.OwnerGroupId != gid;
        // mode is valid and different
        var shouldChangePermissions = mode > 0 && file.FileAccessPermissions != (FileAccessPermissions)mode;

        if (shouldChangeOwner) file.SetOwner(uid, gid);
        if (shouldChangePermissions) file.FileAccessPermissions = (FileAccessPermissions)mode;
        // if we didn't change anything, then return
        if (!shouldChangeOwner && !shouldChangePermissions) return;

        // guarantee immediate flush
        file.Refresh();
            
        // spinwait to ensure the status is updated
        const int Time = 2000;
        const int Interval = 50;
        const int Limit = Time / Interval;
        var count = 0;
        while (file.OwnerUserId != uid || file.OwnerGroupId != gid || file.FileAccessPermissions != (FileAccessPermissions)mode && count < Limit)
        {
            Thread.Sleep(Interval);
            count++;
            file.Refresh();
        }
    }
}
