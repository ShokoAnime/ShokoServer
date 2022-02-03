using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class IntegrityCheckController : BaseController
    {
        [HttpPost]
        public ActionResult<Scan> AddScan(Scan scan)
        {
            if (scan.ScanID == 0)
            {
                SVR_Scan s = new SVR_Scan
                {
                    Status = scan.Status, ImportFolders = scan.ImportFolders, CreationTIme = DateTime.Now
                };
                RepoFactory.Scan.Save(s);
                scan = s;
            }
            List<ScanFile> files = scan.GetImportFolderList()
                .SelectMany(a => RepoFactory.VideoLocalPlace.GetByImportFolder(a))
                .Select(p => new {p, v = p.VideoLocal})
                .Select(t => new ScanFile
                {
                    Hash = t.v.ED2KHash,
                    FileSize = t.v.FileSize,
                    FullName = t.p.FullServerPath,
                    ScanID = scan.ScanID,
                    Status = (int) ScanFileStatus.Waiting,
                    ImportFolderID = t.p.ImportFolderID,
                    VideoLocal_Place_ID = t.p.VideoLocal_Place_ID
                }).ToList();
            RepoFactory.ScanFile.Save(files);
            return scan;
        }

        [HttpGet("{id}/Start")]
        public ActionResult StartScan(int id)
        {
            
            return Ok();
        }
    }
}