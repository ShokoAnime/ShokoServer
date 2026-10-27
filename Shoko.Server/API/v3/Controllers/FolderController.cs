using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[InitFriendly]
[DatabaseBlockedExempt]
[Authorize(Roles = "admin,init")]
public class FolderController : BaseController
{
    private readonly ILogger<FolderController> _logger;

    /// <summary>
    /// The formats of the kernel's own and the desktop's pseudo file systems, as .NET reports
    /// them, with the older names some runtimes report kept beside the current ones.
    /// </summary>
    private static readonly HashSet<string> _excludedFormats = new(StringComparer.Ordinal)
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
        "proc",
        "pstore",
        "pstorefs",
        "ramfs",
        "securityfs",
        "sysfs",
        "tmpfs",
        "tracefs",
    };

    /// <summary>
    /// Where Unix-like systems keep the kernel's views, devices and the boot and EFI partitions;
    /// never where a library lives. Matched by path, so a FAT-formatted drive elsewhere still lists.
    /// </summary>
    private static readonly string[] _excludedUnixRoots = ["/proc", "/sys", "/dev", "/boot", "/efi"];

    /// <summary>
    /// Whether a mount point is part of the system rather than storage a library could live on.
    /// </summary>
    /// <param name="path">The mount point's path.</param>
    /// <param name="format">The file system's format, as .NET reports it.</param>
    /// <returns><see langword="true"/> to leave the mount point out of the list.</returns>
    internal static bool IsSystemMountPoint(string path, string format)
    {
        if (_excludedFormats.Contains(format))
            return true;
        if (!path.StartsWith('/'))
            return false;
        var trimmed = path.Length > 1 ? path.TrimEnd('/') : path;
        return _excludedUnixRoots.Any(root => trimmed == root || trimmed.StartsWith(root + '/', StringComparison.Ordinal));
    }

    public FolderController(ILogger<FolderController> logger, ISettingsProvider settingsProvider) : base(settingsProvider)
    {
        _logger = logger;
    }

    [HttpGet("MountPoints")]
    [HttpGet("Drives")]
    public ActionResult<IEnumerable<Drive>> GetMountPoints()
    {
        return DriveInfo.GetDrives()
            .Select(d =>
            {
                if (d.DriveType == DriveType.Unknown)
                    return null;

                string fullName;
                try
                {
                    fullName = d.RootDirectory.FullName;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An exception occurred while trying to get the full name of the drive: {ex}", ex.Message);
                    return null;
                }

                string driveFormat;
                try
                {
                    driveFormat = d.DriveFormat;
                }
                catch (Exception ex)
                {
                    _logger.LogError("An exception occurred while trying to get the drive format of the drive: {ex}", ex.Message);
                    return null;
                }

                if (IsSystemMountPoint(fullName, driveFormat))
                    return null;

                MountPointSizes? sizes = null;
                try
                {
                    sizes = d.IsReady
                        ? new MountPointSizes()
                        {
                            Files = d.RootDirectory.GetFiles()?.Length ?? 0,
                            Folders = d.RootDirectory.GetDirectories()?.Length ?? 0,
                            AvailableBytes = d.AvailableFreeSpace,
                            TotalBytes = d.TotalSize,
                        }
                        : null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An exception occurred while trying to get the sizes of the drive: {ex}", ex.Message);
                }

                return new Drive()
                {
                    Path = fullName,
                    IsAccessible = sizes != null,
                    Sizes = sizes,
                    Type = d.DriveType,
                };
            })
            .WhereNotNull()
            .DistinctBy(mountPoint => mountPoint.Path)
            .OrderBy(mountPoint => mountPoint.Path)
            .ToList();
    }

    [HttpGet]
    public ActionResult<IEnumerable<Folder>> GetFolder([FromQuery] string path)
    {
        if (!Directory.Exists(path))
        {
            return NotFound("Directory not found");
        }

        var root = new DirectoryInfo(path);
        return root.GetDirectories()
            .Select(dir =>
            {
                ChildItems? childItems = null;
                try
                {
                    childItems = new ChildItems()
                    {
                        Files = dir.GetFiles()?.Length ?? 0,
                        Folders = dir.GetDirectories()?.Length ?? 0
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An exception occurred while trying to get the child items of the directory: {ex}", ex.Message);
                }

                return new Folder() { Path = dir.FullName, IsAccessible = childItems != null, Sizes = childItems };
            })
            .OrderBy(folder => folder.Path)
            .ToList();
    }
}
