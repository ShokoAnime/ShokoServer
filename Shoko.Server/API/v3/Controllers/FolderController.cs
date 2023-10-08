using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private static HashSet<string> ExcludedFormats = new()
    {
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
    };

    [HttpGet("Drives")]
    public ActionResult<IEnumerable<Drive>> GetDrives()
    {
        return DriveInfo.GetDrives().Select(d =>
        {
            if (d.DriveType == DriveType.Unknown)
                return null;

            string fullName;
            try
            {
                fullName = d.RootDirectory.FullName;
            }
            catch
            {
                return null;
            }

            string driveFormat;
            try
            {
                driveFormat = d.DriveFormat;
            }
            catch
            {
                return null;
            }

            foreach (var format in ExcludedFormats)
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
            catch (UnauthorizedAccessException)
            {
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
        return root.GetDirectories().Select(dir =>
        {
            ChildItems childItems = null;
            try
            {
                childItems = new ChildItems()
                {
                    Files = dir.GetFiles()?.Length ?? 0, Folders = dir.GetDirectories()?.Length ?? 0
                };
            }
            catch (UnauthorizedAccessException)
            {
            }

            return new Folder() { Path = dir.FullName, IsAccessible = childItems != null, Sizes = childItems };
        }).ToList();
    }

    public FolderController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
