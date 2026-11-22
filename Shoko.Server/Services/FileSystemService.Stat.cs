using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Mono.Unix.Native;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.Utilities;

namespace Shoko.Server.Services;

public partial class FileSystemService
{
    public FileSystemStat? GetStat(string? path, bool followSymbolicLinks = true)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        if (PlatformUtility.IsWindows)
            return ReadWindowsInfo(path, followSymbolicLinks);

        if (PlatformUtility.IsUnixLike)
            return ReadUnixInfo(path, followSymbolicLinks);

        return null;
    }

    private static string ToHex(UInt128 value)
        => value.ToString("x");

    private enum EntryKind
    {
        File,
        Directory,
        SymbolicLink,
        Special,
    }

    private static FileSystemStat Build(string path, EntryKind kind, StatIdentity identity, long linkCount, DateTime lastWriteTime, DateTime lastAccessTime, DateTime? changeTime, DateTime? creationTime, long size, long? allocatedSize, bool? isDirectoryLink = null, SpecialFileKind specialKind = SpecialFileKind.Generic, string? deviceID = null)
        => kind switch
        {
            EntryKind.Directory => new DirectoryStat
            {
                Path = path,
                Identity = identity,
                LinkCount = linkCount,
                LastWriteTime = lastWriteTime,
                LastAccessTime = lastAccessTime,
                ChangeTime = changeTime,
                CreationTime = creationTime,
            },
            EntryKind.SymbolicLink => new SymbolicLinkStat
            {
                Path = path,
                Identity = identity,
                LinkCount = linkCount,
                LastWriteTime = lastWriteTime,
                LastAccessTime = lastAccessTime,
                ChangeTime = changeTime,
                CreationTime = creationTime,
                Target = new FileInfo(PlatformUtility.EnsureUsablePath(path)).LinkTarget,
                IsDirectoryLink = isDirectoryLink,
            },
            EntryKind.Special => new SpecialFileStat
            {
                Path = path,
                Identity = identity,
                LinkCount = linkCount,
                LastWriteTime = lastWriteTime,
                LastAccessTime = lastAccessTime,
                ChangeTime = changeTime,
                CreationTime = creationTime,
                Kind = specialKind,
                DeviceID = deviceID,
            },
            _ => new FileStat
            {
                Path = path,
                Identity = identity,
                LinkCount = linkCount,
                LastWriteTime = lastWriteTime,
                LastAccessTime = lastAccessTime,
                ChangeTime = changeTime,
                CreationTime = creationTime,
                Size = size,
                AllocatedSize = allocatedSize,
            },
        };

    #region Unix

    internal static volatile bool _statxUnavailable;

    private static FileSystemStat? ReadUnixInfo(string path, bool followSymbolicLinks)
    {
        // Linux answers everything, the creation time included, in one statx call.
        if (!_statxUnavailable && OperatingSystem.IsLinux())
        {
            try
            {
                if (statx(AtFdCwd, path, followSymbolicLinks ? 0 : AtSymlinkNoFollow, StatxBasicStats | StatxBirthTime, out var buffer) is 0)
                    return FromStatx(path, buffer);

                // ENOSYS: a kernel before 4.11. EPERM: a container whose seccomp profile predates statx.
                var errno = Marshal.GetLastPInvokeError();
                if (errno is not (Enosys or Eperm))
                    return errno is Enoent or Enotdir ? null : throw UnixError(path, errno);
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
            {
            }

            _statxUnavailable = true;
        }

        var result = followSymbolicLinks ? Syscall.stat(path, out var stat) : Syscall.lstat(path, out stat);
        if (result is not 0)
        {
            var errno = Stdlib.GetLastError();
            return errno is Errno.ENOENT or Errno.ENOTDIR ? null : throw UnixError(path, NativeConvert.FromErrno(errno));
        }

        var type = (ushort)((uint)stat.st_mode & FileTypeMask);
        var kind = KindOf(type);
        return Build(
            path,
            kind,
            new(ToHex(stat.st_dev), ToHex(stat.st_ino)),
            (long)stat.st_nlink,
            FromUnixTime(stat.st_mtime, stat.st_mtime_nsec),
            FromUnixTime(stat.st_atime, stat.st_atime_nsec),
            FromUnixTime(stat.st_ctime, stat.st_ctime_nsec),
            null,
            stat.st_size,
            stat.st_blocks * 512,
            specialKind: SpecialKindOf(type),
            deviceID: type is CharacterDeviceType or BlockDeviceType ? ToHex(stat.st_rdev) : null
        );
    }

    private static EntryKind KindOf(ushort type)
        => type switch
        {
            RegularFileType => EntryKind.File,
            DirectoryType => EntryKind.Directory,
            SymbolicLinkType => EntryKind.SymbolicLink,
            _ => EntryKind.Special,
        };

    private static SpecialFileKind SpecialKindOf(ushort type)
        => type switch
        {
            PipeType => SpecialFileKind.Pipe,
            SocketType => SpecialFileKind.Socket,
            CharacterDeviceType => SpecialFileKind.CharacterDevice,
            BlockDeviceType => SpecialFileKind.BlockDevice,
            _ => SpecialFileKind.Generic,
        };

    /// <summary>
    ///   A device number put together the way glibc's <c>makedev</c> does, so
    ///   statx gives the same number stat does.
    /// </summary>
    private static ulong MakeDevice(uint major, uint minor)
        => ((ulong)(major & 0xfffff000) << 32) | ((ulong)(major & 0xfff) << 8) | ((ulong)(minor & 0xffffff00) << 12) | (minor & 0xff);

    private static DateTime FromUnixTime(long seconds, long nanoseconds)
        => DateTime.UnixEpoch.AddTicks(seconds * TimeSpan.TicksPerSecond + nanoseconds / 100);

    private static Exception UnixError(string path, int errno)
    {
        var message = $"Could not read {path}: {Marshal.GetPInvokeErrorMessage(errno)}";
        return errno is Eacces ? new UnauthorizedAccessException(message) : new IOException(message, errno);
    }

    private static FileSystemStat? FromStatx(string path, in StatxBuffer buffer)
    {
        var type = (ushort)(buffer.Mode & FileTypeMask);
        return Build(
            path,
            KindOf(type),
            new(ToHex(MakeDevice(buffer.DevMajor, buffer.DevMinor)), ToHex(buffer.Inode)),
            buffer.LinkCount,
            buffer.LastWriteTime.ToDateTime(),
            buffer.LastAccessTime.ToDateTime(),
            buffer.ChangeTime.ToDateTime(),
            (buffer.Mask & StatxBirthTime) is not 0 ? buffer.BirthTime.ToDateTime() : null,
            (long)buffer.Size,
            (buffer.Mask & StatxBlocks) is not 0 ? (long)buffer.Blocks * 512 : null,
            specialKind: SpecialKindOf(type),
            deviceID: type is CharacterDeviceType or BlockDeviceType ? ToHex(MakeDevice(buffer.RdevMajor, buffer.RdevMinor)) : null
        );
    }

    private const int AtFdCwd = -100;

    private const uint StatxBlocks = 0x400;

    private const uint StatxBasicStats = 0x7ff;

    private const uint StatxBirthTime = 0x800;

    private const int AtSymlinkNoFollow = 0x100;

    private const int Enoent = 2;

    private const int Enotdir = 20;

    private const int Eacces = 13;

    private const int Enosys = 38;

    private const int Eperm = 1;

    private const ushort FileTypeMask = 0xf000;

    private const ushort DirectoryType = 0x4000;

    private const ushort RegularFileType = 0x8000;

    private const ushort SymbolicLinkType = 0xa000;

    private const ushort PipeType = 0x1000;

    private const ushort CharacterDeviceType = 0x2000;

    private const ushort BlockDeviceType = 0x6000;

    private const ushort SocketType = 0xc000;

    [DllImport("libc", SetLastError = true)]
    private static extern int statx(int dirfd, [MarshalAs(UnmanagedType.LPUTF8Str)] string pathname, int flags, uint mask, out StatxBuffer buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct StatxTimestamp
    {
        public long Seconds;
        public uint Nanoseconds;
        private readonly int _reserved;

        public readonly DateTime ToDateTime()
            => DateTime.UnixEpoch.AddTicks(Seconds * TimeSpan.TicksPerSecond + Nanoseconds / 100);
    }

    /// <summary>
    ///   <c>struct statx</c> from <c>linux/stat.h</c>, 256 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct StatxBuffer
    {
        [FieldOffset(0)]
        public uint Mask;

        [FieldOffset(16)]
        public uint LinkCount;

        [FieldOffset(28)]
        public ushort Mode;

        [FieldOffset(32)]
        public ulong Inode;

        [FieldOffset(40)]
        public ulong Size;

        [FieldOffset(48)]
        public ulong Blocks;

        [FieldOffset(64)]
        public StatxTimestamp LastAccessTime;

        [FieldOffset(80)]
        public StatxTimestamp BirthTime;

        [FieldOffset(96)]
        public StatxTimestamp ChangeTime;

        [FieldOffset(112)]
        public StatxTimestamp LastWriteTime;

        [FieldOffset(128)]
        public uint RdevMajor;

        [FieldOffset(132)]
        public uint RdevMinor;

        [FieldOffset(136)]
        public uint DevMajor;

        [FieldOffset(140)]
        public uint DevMinor;
    }

    #endregion

    #region Windows

    private static FileSystemStat? ReadWindowsInfo(string path, bool followSymbolicLinks)
    {
        // Backup semantics so a directory opens too. Without the reparse-point flag a link answers for its target.
        var flags = FileFlagBackupSemantics | (followSymbolicLinks ? 0 : FileFlagOpenReparsePoint);
        using var handle = CreateFileW(PlatformUtility.EnsureUsablePath(path), FileReadAttributes, FileShareAll, IntPtr.Zero, OpenExisting, flags, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            return error is ErrorFileNotFound or ErrorPathNotFound ? null : throw WindowsError(path, error);
        }

        if (!GetFileInformationByHandleEx(handle, FileBasicInfoClass, out FILE_BASIC_INFO basic, (uint)Marshal.SizeOf<FILE_BASIC_INFO>()) ||
            !GetFileInformationByHandleEx(handle, FileStandardInfoClass, out FILE_STANDARD_INFO standard, (uint)Marshal.SizeOf<FILE_STANDARD_INFO>()))
            throw WindowsError(path, Marshal.GetLastPInvokeError());

        // FILE_ID_INFO has the full 128-bit ID ReFS needs; NTFS IDs keep their value in the low 64 bits.
        StatIdentity identity;
        if (GetFileInformationByHandleEx(handle, FileIdInfoClass, out FILE_ID_INFO idInfo, (uint)Marshal.SizeOf<FILE_ID_INFO>()))
            identity = new(ToHex(idInfo.VolumeSerialNumber), ToHex(new UInt128(idInfo.FileIdHigh, idInfo.FileIdLow)));
        // Older file systems (FAT) only answer the 64-bit form.
        else if (GetFileInformationByHandle(handle, out var fileInfo))
            identity = new(ToHex(fileInfo.VolumeSerialNumber), ToHex(((ulong)fileInfo.FileIndexHigh << 32) | fileInfo.FileIndexLow));
        else
            throw WindowsError(path, Marshal.GetLastPInvokeError());

        // Opened without following, a reparse point is only a link when it names a target; others (deduplicated
        // or cloud files) are read as the file or directory they stand for.
        var kind = standard.Directory is not 0 ? EntryKind.Directory : EntryKind.File;
        if (!followSymbolicLinks && (basic.FileAttributes & FileAttributeReparsePoint) is not 0 && new FileInfo(PlatformUtility.EnsureUsablePath(path)).LinkTarget is not null)
            kind = EntryKind.SymbolicLink;

        return Build(
            path,
            kind,
            identity,
            standard.NumberOfLinks,
            DateTime.FromFileTimeUtc(basic.LastWriteTime),
            DateTime.FromFileTimeUtc(basic.LastAccessTime),
            basic.ChangeTime > 0 ? DateTime.FromFileTimeUtc(basic.ChangeTime) : null,
            basic.CreationTime > 0 ? DateTime.FromFileTimeUtc(basic.CreationTime) : null,
            standard.EndOfFile,
            standard.AllocationSize,
            // A Windows link is made as a file or a directory link, and keeps the directory flag of its kind.
            isDirectoryLink: kind is EntryKind.SymbolicLink ? standard.Directory is not 0 : null
        );
    }

    private static Exception WindowsError(string path, int error)
    {
        var message = $"Could not read {path}: {Marshal.GetPInvokeErrorMessage(error)}";
        return error is ErrorAccessDenied ? new UnauthorizedAccessException(message) : new IOException(message, unchecked((int)0x80070000 | error));
    }

    private const int ErrorFileNotFound = 2;

    private const int ErrorPathNotFound = 3;

    private const int ErrorAccessDenied = 5;

    private const uint FileFlagOpenReparsePoint = 0x00200000;

    private const uint FileAttributeReparsePoint = 0x400;

    private const uint FileReadAttributes = 0x80;

    private const uint FileShareAll = 0x7;

    private const uint OpenExisting = 3;

    private const uint FileFlagBackupSemantics = 0x02000000;

    private const int FileBasicInfoClass = 0;

    private const int FileStandardInfoClass = 1;

    private const int FileIdInfoClass = 18;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle hFile, int fileInformationClass, out FILE_BASIC_INFO lpFileInformation, uint dwBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle hFile, int fileInformationClass, out FILE_STANDARD_INFO lpFileInformation, uint dwBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle hFile, int fileInformationClass, out FILE_ID_INFO lpFileInformation, uint dwBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_BASIC_INFO
    {
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public long ChangeTime;
        public uint FileAttributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_STANDARD_INFO
    {
        public long AllocationSize;
        public long EndOfFile;
        public uint NumberOfLinks;
        public byte DeletePending;
        public byte Directory;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_ID_INFO
    {
        public ulong VolumeSerialNumber;
        public ulong FileIdLow;
        public ulong FileIdHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    #endregion
}
