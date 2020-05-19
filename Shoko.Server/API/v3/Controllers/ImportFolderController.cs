using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize]
    public class ImportFolderController : BaseController
    {
        /// <summary>
        /// Handle /api/folder/list
        /// List all saved Import Folders
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult<List<ImportFolder>> GetFolders()
        {
            return RepoFactory.ImportFolder.GetAll().Select(a => new ImportFolder(a)).ToList();
        }

        /// <summary>
        /// Handle /api/folder/add
        /// Add Folder to Import Folders repository
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
        /// Handle /api/folder/edit
        /// Edit folder giving full ImportFolder object with ID
        /// </summary>
        /// <returns>APIStatus</returns>
        [HttpPatch]
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
        /// Handle /api/folder/delete
        /// Delete Import Folder out of Import Folder repository
        /// </summary>
        /// <returns>APIStatus</returns>
        [HttpDelete]
        public ActionResult DeleteFolder(ImportFolder folder)
        {
            if (folder.ID == 0) return BadRequest("ID missing");
            
            string res = Importer.DeleteImportFolder(folder.ID);
            return res == string.Empty ? Ok() : InternalError(res);
        }
    }
}