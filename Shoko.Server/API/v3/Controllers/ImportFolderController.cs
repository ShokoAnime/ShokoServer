using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class ImportFolderController : BaseController
{
    /// <summary>
    /// List all Import Folders
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public ActionResult<List<ImportFolder>> GetAllImportFolders()
    {
        return RepoFactory.ImportFolder.GetAll().Select(a => new ImportFolder(a)).ToList();
    }

    /// <summary>
    /// Add an Import Folder. Does not run import on the folder, so you must scan it yourself.
    /// </summary>
    /// <returns>ImportFolder with generated values like ID</returns>
    [Authorize("admin")]
    [HttpPost]
    public ActionResult<ImportFolder> AddImportFolder([FromBody] ImportFolder folder)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (folder.Path == string.Empty)
        {
            return ValidationProblem("The Folder path must not be Empty", nameof(folder.Path));
        }

        try
        {
            var import = folder.GetServerModel();

            var newFolder = RepoFactory.ImportFolder.SaveImportFolder(import);

            return new ImportFolder(newFolder);
        }
        catch (Exception e)
        {
            return InternalError(e.Message);
        }
    }

    /// <summary>
    /// Get the <see cref="ImportFolder"/> by the given <paramref name="folderID"/>.
    /// </summary>
    /// <param name="folderID">Import Folder ID</param>
    /// <returns></returns>
    [HttpGet("{folderID}")]
    public ActionResult<ImportFolder> GetImportFolderByFolderID([FromRoute] int folderID)
    {
        var folder = RepoFactory.ImportFolder.GetByID(folderID);
        if (folder == null)
        {
            return NotFound("Folder not found.");
        }

        return new ImportFolder(folder);
    }

    /// <summary>
    /// Patch the <see cref="ImportFolder"/> by the given <paramref name="folderID"/> using JSON Patch.
    /// </summary>
    /// <param name="folderID">Import Folder ID</param>
    /// <param name="patch">JSON Patch document</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPatch("{folderID}")]
    public ActionResult PatchImportFolderByFolderID([FromRoute] int folderID,
        [FromBody] JsonPatchDocument<ImportFolder> patch)
    {
        if (patch == null)
        {
            return ValidationProblem("Invalid JSON Patch document.");
        }

        var existing = RepoFactory.ImportFolder.GetByID(folderID);
        if (existing == null)
        {
            return NotFound("Folder not found.");
        }

        var patchModel = new ImportFolder(existing);
        patch.ApplyTo(patchModel, ModelState);
        TryValidateModel(patchModel);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var serverModel = patchModel.GetServerModel();
        RepoFactory.ImportFolder.SaveImportFolder(serverModel);

        return Ok();
    }

    /// <summary>
    /// Edit Import Folder. This replaces all values. 
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut]
    public ActionResult EditImportFolder([FromBody] ImportFolder folder)
    {
        if (string.IsNullOrEmpty(folder.Path))
            ModelState.AddModelError(nameof(folder.Path), "Path missing. Import Folders must be a location that exists on the server.");

        if (folder.ID == 0)
            ModelState.AddModelError(nameof(folder.ID), "ID missing. If this is a new Folder, then use POST.");
        else if (RepoFactory.ImportFolder.GetByID(folder.ID) == null)
            ModelState.AddModelError(nameof(folder.ID), "ID invalid. If this is a new Folder, then use POST.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        RepoFactory.ImportFolder.SaveImportFolder(folder.GetServerModel());

        return Ok();
    }

    /// <summary>
    /// Delete an Import Folder
    /// </summary>
    /// <param name="folderID">Import Folder ID</param>
    /// <param name="removeRecords">If this is false, then VideoLocals, DuplicateFiles, and several other things will be left intact. This is for migration of files to new locations.</param>
    /// <param name="updateMyList">Pretty self explanatory. If this is true, and <paramref name="removeRecords"/> is true, then it will update the list status</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{folderID}")]
    public ActionResult DeleteImportFolderByFolderID([FromRoute] int folderID, [FromQuery] bool removeRecords = true,
        [FromQuery] bool updateMyList = true)
    {
        if (folderID == 0)
        {
            return NotFound("Folder not found.");
        }

        if (!removeRecords)
        {
            // These are annoying to clean up later, so do it now. We can easily recreate them.
            RepoFactory.DuplicateFile.Delete(RepoFactory.DuplicateFile.GetByImportFolder1(folderID));
            RepoFactory.DuplicateFile.Delete(RepoFactory.DuplicateFile.GetByImportFolder2(folderID));
            RepoFactory.ImportFolder.Delete(folderID);
            return Ok();
        }

        var importFolder = RepoFactory.ImportFolder.GetByID(folderID);
        if (importFolder == null)
            return NotFound("Folder not found.");

        var errorMessage = Importer.DeleteImportFolder(importFolder.ImportFolderID, updateMyList);
        if (!string.IsNullOrEmpty(errorMessage))
            return InternalError(errorMessage);

        return Ok();
    }

    /// <summary>
    /// Scan a Specific Import Folder. This checks ALL files, not just new ones. Good for cleaning up files in strange states and making drop folders retry moves 
    /// </summary>
    /// <param name="folderID">Import Folder ID</param>
    /// <returns></returns>
    [HttpGet("{folderID}/Scan")]
    public ActionResult ScanImportFolderByFolderID([FromRoute] int folderID)
    {
        var folder = RepoFactory.ImportFolder.GetByID(folderID);
        if (folder == null)
            return NotFound("Folder not found.");

        Importer.RunImport_ScanFolder(folderID);
        return Ok();
    }

    public ImportFolderController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
