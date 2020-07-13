using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class FolderController : Controller
    {
        [HttpGet("drives")]
        public ActionResult<IEnumerable<Drive>> GetDrives()
        {
            return  DriveInfo.GetDrives().Select(d =>
            {
                ChildItems childItems = null;
                try
                {
                    
                    childItems = d.IsReady ? new ChildItems()
                    {
                        Files = d.RootDirectory.GetFiles()?.Length ?? 0,
                        Folders = d.RootDirectory.GetDirectories()?.Length ?? 0
                    } : null;
                }
                catch (UnauthorizedAccessException)
                {
                }

                return new Drive()
                {
                    Path = d.RootDirectory.FullName,
                    CanAccess = childItems != null,
                    Sizes = childItems,
                    DriveType = d.DriveType
                };
            }).ToList();
        }
        
        [HttpGet("")]
        public ActionResult<IEnumerable<Folder>> GetFolder([FromQuery] string path)
        {
            if (!Directory.Exists(path)) return NotFound("Directory not found");
            
            var root  = new DirectoryInfo(path);
            return root.GetDirectories().Select(dir =>
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
                catch (UnauthorizedAccessException)
                {
                }

                return new Folder()
                {
                    Path = dir.FullName,
                    CanAccess = childItems != null,
                    Sizes = childItems
                };
            }).ToList();
        }
    }
}