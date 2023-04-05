
using System;
using System.IO;
using System.Runtime.InteropServices;
using Mono.Unix;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Utilities;

public static class FileSystemUtils
{
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

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

    /// <summary>
    /// Attempts to retrieve the inode number (Unix) or file ID (Windows) of the
    /// file.
    /// </summary>
    /// <remarks>
    /// The inode number is a unique identifier for files on Unix-based systems,
    /// while the file ID serves a similar purpose on Windows. Both are unique
    /// within a specific volume but are not guaranteed to be unique across
    /// different volumes. This method attempts to retrieve the appropriate
    /// platform-specific identifier depending on the system it is running on.
    /// </remarks>
    /// <param name="path">The path of the file for which the unique identifier
    /// is to be obtained.</param>
    /// <returns>
    /// The inode number (Unix) or file ID (Windows) if successful, or null if
    /// the file doesn't exist or the platform-specific identifier cannot be
    /// obtained.
    /// </returns>
    public static long? GetFileUniqueIdentifier(string path)
    {
        if (!File.Exists(path))
            return null;

        switch (Environment.OSVersion.Platform)
        {
            // We're running on Unix, so try to get the inode number.
            case PlatformID.Unix:
            case PlatformID.MacOSX:
                if (UnixFileSystemInfo.TryGetFileSystemEntry(path, out var unixFile))
                    return unixFile.Inode;
                break;
            // We're running on Windows, so try to get the file ID (similar to an inode on Unix, just for Windows).
            case PlatformID.Win32NT:
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    if (GetFileInformationByHandle(fileStream.SafeFileHandle.DangerousGetHandle(), out BY_HANDLE_FILE_INFORMATION fileInfo))
                        return (long)(((ulong)fileInfo.FileIndexHigh << 32) | fileInfo.FileIndexLow);
                break;
        }

        // We couldn't get an unique id for the file for whatever reason.
        return null;
    }

    /// <summary>
    /// Creates a hard link or copy of the file at the specified relative path
    /// in the import folder.
    /// </summary>
    /// <param name="currentLocation">The current file location from which
    /// the hard link or copy should be created.</param>
    /// <param name="nextImportFolder">The import folder where the hard link or
    /// copy should be created.</param>
    /// <param name="relativePath">The relative path for the hard link or copy
    /// within the import folder.</param>
    /// <returns>Returns an instance of SVR_VideoLocal_Place representing the
    /// new file location, or null if the operation fails.</returns>
    public static SVR_VideoLocal_Place CreateHardLinkOrCopy(SVR_VideoLocal_Place currentLocation, SVR_ImportFolder nextImportFolder, string relativePath)
    {
        var file = currentLocation.VideoLocal;
        if (file == null)
            return null;

        var currentImportFolder  = currentLocation.ImportFolder;
        var path = currentLocation.FullServerPath;
        if (!File.Exists(path))
            return null;

        var targetPath = Path.Combine(nextImportFolder.ImportFolderLocation, relativePath);
        if (File.Exists(targetPath))
            // TODO: Decide if this is the best way to handle it.
            return null;

        Utils.ShokoServer.AddFileWatcherExclusion(targetPath);
        try
        {
            FileInfo sourceInfo = new FileInfo(path);
            FileInfo targetInfo;
            switch (Environment.OSVersion.Platform)
            {
                // We're running on Unix, so use the Mono.Unix nuget to create a hard-link.
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    try
                    {
                        var unixFileInfo = new UnixFileInfo(path);
                        var unixCopyInfo = unixFileInfo.CreateLink(targetPath);
                        if (unixCopyInfo == null)
                            return null;
                        targetInfo = new FileInfo(targetPath);
                    }
                    catch
                    {
                        targetInfo = sourceInfo.CopyTo(targetPath);
                    }
                    break;
                // We're running on Windows, so use a dll-import of the kernel call to create a hard-link.
                case PlatformID.Win32NT:
                    if (CreateHardLink(targetPath, path, IntPtr.Zero))
                        targetInfo = new FileInfo(targetPath);
                    else
                        targetInfo = sourceInfo.CopyTo(targetPath);
                    break;
                // Just ignore any other platforms.
                default:
                    return null;
            }

            // Create a new file-name-hash entry if none exists.
            var fileName = Path.GetFileName(relativePath);
            if (fileName != Path.GetFileName(currentLocation.FileName))
            {
                var fileNameHash = new FileNameHash()
                {
                    FileName = fileName,
                    FileSize = file.FileSize,
                    Hash = file.Hash,
                };
                RepoFactory.FileNameHash.Save(fileNameHash);
            }

            // Create the new location entry.
            var newLocation = new SVR_VideoLocal_Place
            {
                FilePath = relativePath,
                ImportFolderID = nextImportFolder.ImportFolderID,
                ImportFolderType = nextImportFolder.ImportFolderType,
                AllowAutoDelete = false,
            };
            RepoFactory.VideoLocalPlace.Save(newLocation);
            currentLocation.AllowAutoDelete = false;
            RepoFactory.VideoLocalPlace.Save(currentLocation);

            return newLocation;
        }
        finally {
            Utils.ShokoServer.RemoveFileWatcherExclusion(targetPath);
        }
    }
}
