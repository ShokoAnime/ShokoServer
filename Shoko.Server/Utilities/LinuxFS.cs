using System;
using System.Runtime.InteropServices;

namespace Shoko.Server.Utilities
{
    public static class LinuxFS
    {
        private static bool CanRun() => Environment.OSVersion.Platform == PlatformID.MacOSX || Environment.OSVersion.Platform == PlatformID.Unix;

        public static void SetLinuxPermissions(string path, int uid, int gid, int mode)
        {
            if (!CanRun()) return;

            Native.chown(path, uid, gid);
            if (mode > 0)
                Native.chmod(path, mode);
        }

        public static class Native
        {
            [DllImport("libc", SetLastError = true)]
            public static extern int chown(string path, int owner, int group);

            [DllImport("libc", SetLastError = true)]
            internal static extern int chmod(string path, int mode);
        }
    }
}
