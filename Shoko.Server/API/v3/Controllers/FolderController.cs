using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.FileSystem.Services;
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

    private readonly IFileSystem _fileSystem;

    public FolderController(ILogger<FolderController> logger, ISettingsProvider settingsProvider, IFileSystem fileSystem) : base(settingsProvider)
    {
        _logger = logger;
        _fileSystem = fileSystem;
    }

    [HttpGet("MountPoints")]
    [HttpGet("Drives")]
    public ActionResult<IEnumerable<Drive>> GetMountPoints()
    {
        return _fileSystem.GetVolumeMounts()
            .Select(mount =>
            {
                var fullName = mount.MountPoint;
                MountPointSizes? sizes = null;
                try
                {
                    var d = new DriveInfo(fullName);
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
                    Type = mount.Kind switch
                    {
                        VolumeKind.Fixed => DriveType.Fixed,
                        VolumeKind.Removable => DriveType.Removable,
                        VolumeKind.Network => DriveType.Network,
                        VolumeKind.Optical => DriveType.CDRom,
                        VolumeKind.Memory => DriveType.Ram,
                        _ => DriveType.Unknown,
                    },
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
        if (_fileSystem.GetDirectoryInfo(path) is not { } root)
        {
            return NotFound("Directory not found");
        }

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
