using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Mono.Unix.Native;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.Utilities;

namespace Shoko.Server.Services;

public partial class FileSystemService
{
    public VolumeStat? GetVolumeStat(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // The entry's own volume, as its identity gives it, so the two compare directly.
        if (GetStat(path) is not { } entry)
            return null;

        var fullPath = Path.GetFullPath(path);
        if (PlatformUtility.IsWindows)
            return ReadWindowsVolume(fullPath, entry.Identity.VolumeID);

        if (PlatformUtility.IsUnixLike)
            return ReadUnixVolume(fullPath, entry.Identity.VolumeID);

        return null;
    }

    public IReadOnlyList<VolumeMount> GetVolumeMounts(bool includeSystem = false, bool includeStats = false)
    {
        IEnumerable<VolumeMount> mounts;
        if (OperatingSystem.IsLinux())
        {
            // A later mount at the same place hides the earlier one.
            mounts = ReadLinuxMounts()
                .GroupBy(mount => mount.MountPoint, StringComparer.Ordinal)
                .Select(group => group.Last())
                .Select(mount => new VolumeMount
                {
                    MountPoint = mount.MountPoint,
                    Source = mount.Source,
                    FormatName = mount.Format,
                    Kind = KindOfLinuxMount(mount),
                    IsReadOnly = mount.Options.Split(',').Contains("ro"),
                    IsSystem = IsSystemMountPoint(mount.MountPoint, mount.Format),
                });
        }
        else
        {
            mounts = DriveInfo.GetDrives().Select(ReadDriveMount).WhereNotNull();
        }

        return mounts
            .Where(mount => includeSystem || !mount.IsSystem)
            .OrderBy(mount => mount.MountPoint, StringComparer.Ordinal)
            .Select(mount => includeStats ? TryReadVolume(mount) : mount)
            .ToList();
    }

    /// <summary>
    ///   Reads a listed mount's volume, keeping the mount as listed when it
    ///   cannot be read, so one bad share does not fail the list.
    /// </summary>
    private VolumeMount TryReadVolume(VolumeMount mount)
    {
        try
        {
            return GetVolumeStat(mount.MountPoint) is { } stat ? stat with { IsSystem = mount.IsSystem } : mount;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not read the volume mounted at {MountPoint}", mount.MountPoint);
            return mount;
        }
    }

    /// <summary>
    ///   Reads a drive from .NET's list, or <c>null</c> when it cannot be read
    ///   or, on Windows, is not ready (a card reader without a card).
    /// </summary>
    private static VolumeMount? ReadDriveMount(DriveInfo drive)
    {
        string mountPoint, format;
        try
        {
            mountPoint = drive.RootDirectory.FullName;
            format = drive.DriveFormat;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var kind = KindOf(drive.DriveType);
        string? source = null;
        if (PlatformUtility.IsWindows && kind is VolumeKind.Network && mountPoint.Length >= 2 && mountPoint[1] is ':')
        {
            var connection = new StringBuilder(MaxPathLength);
            var length = (uint)connection.Capacity;
            if (WNetGetConnectionW(mountPoint[..2], connection, ref length) is 0)
                source = connection.ToString();
        }

        return new()
        {
            MountPoint = mountPoint,
            Source = source,
            FormatName = format,
            Kind = kind,
            IsSystem = drive.DriveType is DriveType.Unknown || IsSystemMountPoint(mountPoint, format),
        };
    }

    /// <summary>
    ///   The formats of the kernel's own and the desktop's pseudo file
    ///   systems, with the older names some runtimes report kept beside the
    ///   current ones.
    /// </summary>
    private static readonly HashSet<string> _systemFormats = new(StringComparer.Ordinal)
    {
        "autofs",
        "binfmt_misc",
        "bpf",
        "bpf_fs",
        "cgroup",
        "cgroup2",
        "cgroup2fs",
        "configfs",
        "debugfs",
        "devpts",
        "devtmpfs",
        "efivarfs",
        "fuse.gvfsd-fuse",
        "fuse.portal",
        "fusectl",
        "hugetlbfs",
        "mqueue",
        "nsfs",
        "proc",
        "pstore",
        "pstorefs",
        "ramfs",
        "rpc_pipefs",
        "securityfs",
        "sysfs",
        "tmpfs",
        "tracefs",
    };

    /// <summary>
    ///   Where Unix-like systems keep the kernel's views, devices and the boot
    ///   and EFI partitions; never where a library lives. Matched by path, so
    ///   a FAT-formatted drive elsewhere still lists.
    /// </summary>
    private static readonly string[] _systemUnixRoots = ["/proc", "/sys", "/dev", "/boot", "/efi"];

    /// <summary>
    ///   Whether a mount point is part of the system rather than storage a
    ///   library could live on.
    /// </summary>
    /// <param name="path">The mount point's path.</param>
    /// <param name="format">The file system's format.</param>
    /// <returns><c>true</c> for the system's own mounts.</returns>
    internal static bool IsSystemMountPoint(string path, string format)
    {
        if (_systemFormats.Contains(format))
            return true;
        if (!path.StartsWith('/'))
            return false;
        var trimmed = path.Length > 1 ? path.TrimEnd('/') : path;
        return _systemUnixRoots.Any(root => trimmed == root || trimmed.StartsWith(root + '/', StringComparison.Ordinal));
    }

    #region Unix

    private static readonly HashSet<string> _networkFormats = new(StringComparer.Ordinal)
    {
        "cifs", "smb3", "smbfs", "nfs", "nfs4", "ncpfs", "afs", "ceph", "glusterfs", "fuse.glusterfs", "fuse.sshfs", "sshfs", "fuse.rclone", "davfs", "fuse.davfs2", "9p",
    };

    private static readonly HashSet<string> _memoryFormats = new(StringComparer.Ordinal) { "tmpfs", "ramfs", "devtmpfs" };

    private static readonly HashSet<string> _opticalFormats = new(StringComparer.Ordinal) { "iso9660", "udf" };

    /// <summary>
    ///   Local file systems whose source names a pool rather than a device.
    /// </summary>
    private static readonly HashSet<string> _localPoolFormats = new(StringComparer.Ordinal) { "zfs", "bcachefs" };

    private static VolumeStat ReadUnixVolume(string fullPath, string volumeID)
    {
        if (Syscall.statvfs(fullPath, out var space) is not 0)
            throw UnixError(fullPath, NativeConvert.FromErrno(Stdlib.GetLastError()));

        // Mount points are matched on the real path, with every link along it followed.
        var realPath = Mono.Unix.UnixPath.GetCompleteRealPath(fullPath) ?? fullPath;
        string mountPoint;
        string? source = null, format = null;
        var kind = VolumeKind.Generic;
        if (OperatingSystem.IsLinux() && FindLinuxMount(realPath) is { } mount)
        {
            mountPoint = mount.MountPoint;
            source = mount.Source;
            format = mount.Format;
            kind = KindOfLinuxMount(mount);
        }
        else
        {
            // Elsewhere .NET's own drive list tells the mount point and the format.
            var drive = DriveInfo.GetDrives()
                .Where(drive => IsUnder(realPath, drive.RootDirectory.FullName))
                .MaxBy(drive => drive.RootDirectory.FullName.Length);
            mountPoint = drive?.RootDirectory.FullName ?? "/";
            if (drive is not null)
            {
                format = drive.DriveFormat;
                kind = KindOf(drive.DriveType);
            }
        }

        var blockSize = (long)space.f_frsize;
        return new()
        {
            Path = fullPath,
            VolumeID = volumeID,
            MountPoint = mountPoint,
            Source = source,
            FormatName = format,
            Kind = kind,
            IsReadOnly = (space.f_flag & MountFlags.ST_RDONLY) is not 0,
            IsSystem = IsSystemMountPoint(mountPoint, format ?? string.Empty),
            TotalSize = (long)space.f_blocks * blockSize,
            FreeSpace = (long)space.f_bfree * blockSize,
            AvailableFreeSpace = (long)space.f_bavail * blockSize,
        };
    }

    private sealed record LinuxMount(string MountPoint, string DeviceNumber, string Format, string Source, string Options);

    /// <summary>
    ///   Reads <c>/proc/self/mountinfo</c>, in the order of the mounts.
    /// </summary>
    private static IEnumerable<LinuxMount> ReadLinuxMounts()
    {
        foreach (var line in File.ReadLines("/proc/self/mountinfo"))
        {
            // ID, parent ID, major:minor, root, mount point, options, optional fields…, "-", format, source, super options.
            var fields = line.Split(' ');
            var separator = Array.IndexOf(fields, "-", 6);
            if (fields.Length < 7 || separator < 0 || separator + 2 >= fields.Length)
                continue;

            yield return new(UnescapeMountField(fields[4]), fields[2], fields[separator + 1], UnescapeMountField(fields[separator + 2]), fields[5]);
        }
    }

    /// <summary>
    ///   Finds the mount a path is under in <c>/proc/self/mountinfo</c>: the
    ///   one with the longest mount point holding it, the last of equals,
    ///   since a later mount hides an earlier one at the same place.
    /// </summary>
    private static LinuxMount? FindLinuxMount(string realPath)
    {
        LinuxMount? found = null;
        foreach (var mount in ReadLinuxMounts())
        {
            if (IsUnder(realPath, mount.MountPoint) && (found is null || mount.MountPoint.Length >= found.MountPoint.Length))
                found = mount;
        }

        return found;
    }

    private static VolumeKind KindOfLinuxMount(LinuxMount mount)
    {
        if (_networkFormats.Contains(mount.Format))
            return VolumeKind.Network;
        if (_memoryFormats.Contains(mount.Format))
            return VolumeKind.Memory;
        if (_opticalFormats.Contains(mount.Format))
            return VolumeKind.Optical;
        if (_localPoolFormats.Contains(mount.Format))
            return VolumeKind.Fixed;
        if (!mount.Source.StartsWith("/dev/", StringComparison.Ordinal))
            return VolumeKind.Generic;

        // The disk says whether it can be taken out; a partition's disk is the folder above it.
        foreach (var flagPath in new[] { $"/sys/dev/block/{mount.DeviceNumber}/removable", $"/sys/dev/block/{mount.DeviceNumber}/../removable" })
        {
            try
            {
                if (File.Exists(flagPath))
                    return File.ReadAllText(flagPath).Trim() is "1" ? VolumeKind.Removable : VolumeKind.Fixed;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return VolumeKind.Fixed;
    }

    /// <summary>
    ///   Reads a mountinfo field, where a space, tab, newline and backslash
    ///   are written as three octal digits after a backslash.
    /// </summary>
    private static string UnescapeMountField(string value)
    {
        if (!value.Contains('\\'))
            return value;

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is '\\' && IsOctal(value, i + 1))
            {
                builder.Append((char)Convert.ToInt32(value.Substring(i + 1, 3), 8));
                i += 3;
                continue;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    private static bool IsOctal(string value, int start)
        => start + 3 <= value.Length && value.AsSpan(start, 3).IndexOfAnyExceptInRange('0', '7') < 0;

    private static bool IsUnder(string path, string mountPoint)
        => mountPoint is "/" ||
            string.Equals(path, mountPoint, PlatformUtility.StringComparison) ||
            path.StartsWith(mountPoint.EndsWith(Path.DirectorySeparatorChar) ? mountPoint : mountPoint + Path.DirectorySeparatorChar, PlatformUtility.StringComparison);

    #endregion

    #region Windows

    private static VolumeStat ReadWindowsVolume(string fullPath, string volumeID)
    {
        var usablePath = PlatformUtility.EnsureUsablePath(fullPath);
        var mountPointBuffer = new StringBuilder(MaxPathLength);
        if (!GetVolumePathNameW(usablePath, mountPointBuffer, (uint)mountPointBuffer.Capacity))
            throw WindowsError(fullPath, Marshal.GetLastPInvokeError());

        var mountPoint = mountPointBuffer.ToString();
        var label = new StringBuilder(MaxPathLength);
        var format = new StringBuilder(MaxPathLength);
        var hasInformation = GetVolumeInformationW(mountPoint, label, (uint)label.Capacity, out _, out _, out var flags, format, (uint)format.Capacity);
        if (!GetDiskFreeSpaceExW(mountPoint, out var available, out var total, out var free))
            throw WindowsError(fullPath, Marshal.GetLastPInvokeError());

        var kind = GetDriveTypeW(mountPoint) switch
        {
            DriveRemovable => VolumeKind.Removable,
            DriveFixed => VolumeKind.Fixed,
            DriveRemote => VolumeKind.Network,
            DriveOptical => VolumeKind.Optical,
            DriveRamDisk => VolumeKind.Memory,
            _ => VolumeKind.Generic,
        };

        // A share mounted by its UNC path names itself; a drive letter mapped to one is asked for its share.
        string? source = null;
        if (mountPoint.StartsWith(@"\\", StringComparison.Ordinal) && !mountPoint.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            source = mountPoint.TrimEnd('\\');
        }
        else if (kind is VolumeKind.Network && mountPoint.Length >= 2 && mountPoint[1] is ':')
        {
            var connection = new StringBuilder(MaxPathLength);
            var length = (uint)connection.Capacity;
            if (WNetGetConnectionW(mountPoint[..2], connection, ref length) is 0)
                source = connection.ToString();
        }

        return new()
        {
            Path = fullPath,
            VolumeID = volumeID,
            MountPoint = mountPoint,
            Source = source,
            FormatName = hasInformation && format.Length > 0 ? format.ToString() : null,
            Label = hasInformation && label.Length > 0 ? label.ToString() : null,
            Kind = kind,
            IsReadOnly = hasInformation && (flags & FileReadOnlyVolume) is not 0,
            IsSystem = kind is VolumeKind.Generic,
            TotalSize = (long)total,
            FreeSpace = (long)free,
            AvailableFreeSpace = (long)available,
        };
    }

    private static VolumeKind KindOf(DriveType type)
        => type switch
        {
            DriveType.Removable => VolumeKind.Removable,
            DriveType.Fixed => VolumeKind.Fixed,
            DriveType.Network => VolumeKind.Network,
            DriveType.CDRom => VolumeKind.Optical,
            DriveType.Ram => VolumeKind.Memory,
            _ => VolumeKind.Generic,
        };

    private const int MaxPathLength = 32768;

    private const uint FileReadOnlyVolume = 0x00080000;

    private const uint DriveRemovable = 2;

    private const uint DriveFixed = 3;

    private const uint DriveRemote = 4;

    private const uint DriveOptical = 5;

    private const uint DriveRamDisk = 6;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetVolumePathNameW(string lpszFileName, StringBuilder lpszVolumePathName, uint cchBufferLength);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetVolumeInformationW(string lpRootPathName, StringBuilder lpVolumeNameBuffer, uint nVolumeNameSize, out uint lpVolumeSerialNumber, out uint lpMaximumComponentLength, out uint lpFileSystemFlags, StringBuilder lpFileSystemNameBuffer, uint nFileSystemNameSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetDiskFreeSpaceExW(string lpDirectoryName, out ulong lpFreeBytesAvailable, out ulong lpTotalNumberOfBytes, out ulong lpTotalNumberOfFreeBytes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetDriveTypeW(string lpRootPathName);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetGetConnectionW(string lpLocalName, StringBuilder lpRemoteName, ref uint lpnLength);

    #endregion
}
