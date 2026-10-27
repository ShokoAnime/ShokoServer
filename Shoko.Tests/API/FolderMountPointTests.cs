using Shoko.Server.API.v3.Controllers;
using Xunit;

namespace Shoko.Tests.API;

public class FolderMountPointTests
{
    // Formats and paths as .NET reported them on a desktop Linux machine.
    [Theory]
    [InlineData("/dev", "devtmpfs")]
    [InlineData("/sys/fs/cgroup", "cgroup2")]
    [InlineData("/sys/fs/pstore", "pstore")]
    [InlineData("/sys/fs/bpf", "bpf")]
    [InlineData("/sys/firmware/efi/efivars", "efivarfs")]
    [InlineData("/sys/kernel/tracing", "tracefs")]
    [InlineData("/proc/sys/fs/binfmt_misc", "autofs")]
    [InlineData("/boot/esp", "vfat")]
    [InlineData("/efi", "vfat")]
    [InlineData("/run/user/1000/gvfs", "fuse.gvfsd-fuse")]
    [InlineData("/run/user/1000/doc", "fuse.portal")]
    [InlineData("/tmp", "tmpfs")]
    public void SystemMountPointsAreLeftOut(string path, string format)
        => Assert.True(FolderController.IsSystemMountPoint(path, format));

    [Theory]
    [InlineData("/", "btrfs")]
    [InlineData("/storage/data", "btrfs")]
    [InlineData("/storage/video", "cifs")]
    [InlineData("/run/media/user/USB", "vfat")]
    [InlineData("/mnt/library", "ext4")]
    [InlineData("/devices", "ext4")]
    [InlineData("C:\\", "NTFS")]
    [InlineData("D:\\", "FAT32")]
    public void StorageIsListed(string path, string format)
        => Assert.False(FolderController.IsSystemMountPoint(path, format));
}
