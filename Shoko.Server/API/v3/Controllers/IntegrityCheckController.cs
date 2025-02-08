using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class IntegrityCheckController : BaseController
{
    [HttpPost]
    public ActionResult<IntegrityCheck> AddScan(IntegrityCheck check)
    {
        var scan = check.ID is > 0 ? RepoFactory.Scan.GetByID(check.ID) : new()
        {
            Status = check.Status,
            ImportFolders = check.ImportFolderIDs.Select(a => a.ToString()).Join(','),
            CreationTIme = DateTime.Now,
        };
        if (scan.ScanID == 0)
            RepoFactory.Scan.Save(scan);

        var files = scan.GetImportFolderList()
            .SelectMany(RepoFactory.VideoLocalPlace.GetByImportFolder)
            .Select(p => new { p, v = p.VideoLocal })
            .Select(t => new ScanFile
            {
                Hash = t.v.Hash,
                FileSize = t.v.FileSize,
                FullName = t.p.FullServerPath,
                ScanID = scan.ScanID,
                Status = (int)ScanFileStatus.Waiting,
                ImportFolderID = t.p.ImportFolderID,
                VideoLocal_Place_ID = t.p.VideoLocal_Place_ID
            }).ToList();
        RepoFactory.ScanFile.Save(files);

        return new IntegrityCheck()
        {
            ID = scan.ScanID,
            ImportFolderIDs = scan.GetImportFolderList(),
            Status = scan.Status,
            CreatedAt = scan.CreationTIme,
        };
    }

    [HttpGet("{id}/Start")]
    public ActionResult StartScan(int id)
    {
        return Ok();
    }

    public IntegrityCheckController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
