using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Legacy;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
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
            ImportFolders = check.ManagedFolderIDs.Select(a => a.ToString()).Join(','),
            CreationTIme = DateTime.Now,
        };
        if (scan.ScanID == 0)
            RepoFactory.Scan.Save(scan);

        var files = scan.ImportFolders.Split(',')
            .Select(int.Parse)
            .SelectMany(RepoFactory.VideoLocalPlace.GetByManagedFolderID)
            .Select(p => new { p, v = p.VideoLocal })
            .Select(t => new ScanFile
            {
                Hash = t.v.Hash,
                FileSize = t.v.FileSize,
                FullName = t.p.Path,
                ScanID = scan.ScanID,
                Status = ScanFileStatus.Waiting,
                ImportFolderID = t.p.ManagedFolderID,
                VideoLocal_Place_ID = t.p.ID
            }).ToList();
        RepoFactory.ScanFile.Save(files);

        return new IntegrityCheck()
        {
            ID = scan.ScanID,
            ManagedFolderIDs = scan.ImportFolders.Split(',')
                .Select(int.Parse)
                .ToList(),
            Status = scan.Status,
            CreatedAt = scan.CreationTIme,
        };
    }

    [HttpPost("{scanID}/Start")]
    public ActionResult StartScan(int scanID)
    {
        return Ok();
    }

    public IntegrityCheckController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
