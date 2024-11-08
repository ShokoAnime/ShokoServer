using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class FolderController : BaseController
{
    private readonly ILogger<FolderController> _logger;

    private static readonly HashSet<string> _excludedFormats =
    [
        "msdos", // fat32 - might be overkill, but the esp (u)efi partition is usually formatted as such.
        "ramfs",
        "configfs",
        "fusectl",
        "tracefs",
        "hugetlbfs",
        "mqueue",
        "debugfs",
        "binfmt_misc",
        "devpts",
        "pstorefs",
        "bpf_fs",
        "cgroup2fs",
        "securityfs",
        "proc",
        "tmpfs",
        "sysfs",
    ];

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

                foreach (var format in _excludedFormats)
                {
                    if (driveFormat == format)
                        return null;
                }

                ChildItems childItems = null;
                try
                {
                    childItems = d.IsReady
                        ? new ChildItems()
                        {
                            Files = d.RootDirectory.GetFiles()?.Length ?? 0,
                            Folders = d.RootDirectory.GetDirectories()?.Length ?? 0,
                        }
                        : null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An exception occurred while trying to get the child items of the drive: {ex}", ex.Message);
                }

                return new Drive()
                {
                    Path = fullName,
                    IsAccessible = childItems != null,
                    Sizes = childItems,
                    Type = d.DriveType,
                };
            })
            .Where(mountPoint => mountPoint != null)
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
                ChildItems childItems = null;
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
