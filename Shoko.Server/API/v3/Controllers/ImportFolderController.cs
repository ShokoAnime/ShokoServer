using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class ImportFolderController : BaseController
    {
        /// <summary>
        /// List all Import Folders
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult<List<ImportFolder>> GetFolders()
        {
            return RepoFactory.ImportFolder.GetAll().Select(a => new ImportFolder(a)).ToList();
        }

        /// <summary>
        /// Add an Import Folder. Does not run import on the folder, so you must scan it yourself.
        /// </summary>
        /// <returns>ImportFolder with generated values like ID</returns>
        [HttpPost]
        public ActionResult<ImportFolder> AddFolder(ImportFolder folder)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (folder.Path == string.Empty)
                return BadRequest("The Folder path must not be Empty");
            try
            {
                Shoko.Models.Server.ImportFolder import = folder.GetServerModel();

                var newFolder = RepoFactory.ImportFolder.SaveImportFolder(import);

                return new ImportFolder(newFolder);
            }
            catch (Exception e)
            {
                return InternalError(e.Message);
            }
        }

        /// <summary>
        /// Patch an Import Folder with JSON Patch.
        /// </summary>
        /// <param name="id">Import Folder ID</param>
        /// <param name="folder">JSON Patch document</param>
        /// <returns></returns>
        [HttpPatch("{id}")]
        public ActionResult PatchImportFolder(int id, [FromBody] JsonPatchDocument<ImportFolder> folder)
        {
            if (folder == null) return BadRequest("object is invalid.");
            var existing = RepoFactory.ImportFolder.GetByID(id);
            if (existing == null) return BadRequest("No Import Folder with ID");
            var patchModel = new ImportFolder(existing);
            folder.ApplyTo(patchModel, ModelState);
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var serverModel = patchModel.GetServerModel();
            RepoFactory.ImportFolder.SaveImportFolder(serverModel);
            return Ok();
        }

        /// <summary>
        /// Edit Import Folder. This replaces all values. 
        /// </summary>
        /// <returns>APIStatus</returns>
        [HttpPut]
        public ActionResult EditFolder(ImportFolder folder)
        {
            if (string.IsNullOrEmpty(folder.Path))
                return BadRequest("Path missing. Import Folders must be a location that exists on the server");

            if (folder.ID == 0)
                return BadRequest("ID missing. If this is a new Folder, then use POST");

            try
            {
                RepoFactory.ImportFolder.SaveImportFolder(folder.GetServerModel());
                return Ok();
            }
            catch (Exception e)
            {
                return InternalError(e.Message);
            }
        }

        /// <summary>
        /// Delete an Import Folder. This removes records and send deleted commands to AniDB, so don't use it frivolously
        /// </summary>
        /// <param name="id">Import Folder ID</param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public ActionResult DeleteFolder(int id)
        {
            if (id == 0) return BadRequest("ID missing");
            
            string res = Importer.DeleteImportFolder(id);
            return res == string.Empty ? Ok() : InternalError(res);
        }

        /// <summary>
        /// Scan a Specific Import Folder. This checks ALL files, not just new ones. Good for cleaning up files in strange states and making drop folders retry moves 
        /// </summary>
        /// <param name="id">Import Folder ID</param>
        /// <returns></returns>
        [HttpGet("{id}/Scan")]
        public ActionResult ScanImportFolder(int id)
        {
            var folder = RepoFactory.ImportFolder.GetByID(id);
            if (folder == null) return BadRequest("No Import Folder with ID");
            Importer.RunImport_ScanFolder(id);
            return Ok();
        }
    }
}