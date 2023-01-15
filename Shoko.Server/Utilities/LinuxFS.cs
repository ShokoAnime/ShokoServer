using System;
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

        // if uid is not valid (<0), skip
        // if uid = 0 (trying to set as root), and we are not root (RealUser.UserId != 0), then set it to the current user
        // if uid = 0 and we are root (RealUser.UserId = 0), then nothing changes, and it can use those values
        if (uid == 0 && RealUser.UserId != 0)
            uid = RealUser.UserId;

        if (gid == 0 && RealUser.GroupId != 0)
            gid = RealUser.GroupId;

        var file = new UnixFileInfo(path);
        var newMode = (FileAccessPermissions)Convert.ToInt32(mode.ToString(), 8);
        // new user ID is valid (>= 0) and has changed (or same for group ID)
        var shouldChangeOwner = uid >= 0 && uid != file.OwnerUserId || gid >= 0 && file.OwnerGroupId != gid;
        // mode is valid and different
        var shouldChangePermissions = mode > 0 && file.FileAccessPermissions != newMode;

        if (shouldChangeOwner) file.SetOwner(uid, gid);
        if (shouldChangePermissions) file.FileAccessPermissions = newMode;
        // if we didn't change anything, then return
        if (!shouldChangeOwner && !shouldChangePermissions) return;

        // guarantee immediate flush
        file.Refresh();
    }
}
