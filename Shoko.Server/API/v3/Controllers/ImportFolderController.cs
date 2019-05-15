using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
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
        /// <returns>List<ImportFolder></returns>
        [HttpGet]
        public ActionResult<IEnumerable<ImportFolder>> GetFolders() => new ShokoServiceImplementation().GetImportFolders();

        /// <summary>
        /// Handle /api/folder/add
        /// Add Folder to Import Folders repository
        /// </summary>
        /// <returns>ImportFolder with generated values like ID</returns>
        [HttpPost]
        public ActionResult<ImportFolder> AddFolder(ImportFolder folder)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (folder.ImportFolderLocation == string.Empty)
                return new APIMessage(StatusCodes.Status400BadRequest,
                    "Bad Request: The Folder path must not be Empty");
            try
            {
                return RepoFactory.ImportFolder.SaveImportFolder(folder);
            }
            catch (Exception e)
            {
                return APIStatus.InternalError(e.Message);
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
            if (String.IsNullOrEmpty(folder.ImportFolderLocation) || folder.ImportFolderID == 0)
                return new APIMessage(400, "ImportFolderLocation and ImportFolderID missing");

            if (folder.IsDropDestination == 1 && folder.IsDropSource == 1)
                return new APIMessage(StatusCodes.Status409Conflict,
                    "The Import Folder can't be both Destination and Source");

            if (folder.ImportFolderID == 0)
                return new APIMessage(StatusCodes.Status409Conflict, "The Import Folder must have an ID");

            try
            {
                RepoFactory.ImportFolder.SaveImportFolder(folder);
                return Ok();
            }
            catch (Exception e)
            {
                return APIStatus.InternalError(e.Message);
            }
        }

        /// <summary>
        /// Handle /api/folder/delete
        /// Delete Import Folder out of Import Folder repository
        /// </summary>
        /// <returns>APIStatus</returns>
        [HttpDelete]
        public ActionResult DeleteFolder(int folderId)
        {
            if (folderId != 0)
            {
                string res = Importer.DeleteImportFolder(folderId);
                if (res == string.Empty)
                {
                    return APIStatus.OK();
                }
                return new APIMessage(500, res);
            }
            return new APIMessage(400, "ImportFolderID missing");
        }
    }
}