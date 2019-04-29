using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;

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
        /// <returns>APIStatus</returns>
        [HttpPost]
        public ActionResult AddFolder(ImportFolder folder)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (folder.ImportFolderLocation != string.Empty)
            {
                try
                {
                    // TODO Do this correctly without calling APIv1
                    CL_Response<ImportFolder> response = new ShokoServiceImplementation().SaveImportFolder(folder);

                    if (string.IsNullOrEmpty(response.ErrorMessage))
                    {
                        return APIStatus.OK();
                    }
                    return new APIMessage(500, response.ErrorMessage);
                }
                catch
                {
                    return APIStatus.InternalError();
                }
            }
            return new APIMessage(400, "Bad Request: The Folder path must not be Empty");
        }

        /// <summary>
        /// Handle /api/folder/edit
        /// Edit folder giving full ImportFolder object with ID
        /// </summary>
        /// <returns>APIStatus</returns>
        [HttpPatch]
        public ActionResult EditFolder(ImportFolder folder)
        {
            if (!String.IsNullOrEmpty(folder.ImportFolderLocation) && folder.ImportFolderID != 0)
            {
                try
                {
                    // TODO Do this correctly without calling APIv1
                    if (folder.IsDropDestination == 1 && folder.IsDropSource == 1)
                    {
                        return new APIMessage(409, "The Import Folder can't be both Destination and Source");
                    }

                    if (folder.ImportFolderID == 0) return new APIMessage(409, "The Import Folder must have an ID");
                    CL_Response<ImportFolder> response =
                        new ShokoServiceImplementation().SaveImportFolder(folder);

                    if (!string.IsNullOrEmpty(response.ErrorMessage)) return new APIMessage(500, response.ErrorMessage);

                    return APIStatus.OK();
                }
                catch
                {
                    return APIStatus.InternalError();
                }
            }
            return new APIMessage(400, "ImportFolderLocation and ImportFolderID missing");
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