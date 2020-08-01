using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Controllers
{
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [Authorize("admin")]
    public class UserController : BaseController
    {
        /// <summary>
        /// List all Users. Admin only
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public ActionResult<List<User>> GetUsers()
        {
            return RepoFactory.JMMUser.GetAll().Select(a => new User(a)).ToList();
        }

        /// <summary>
        /// Add a User. Admin only
        /// </summary>
        /// <returns>User with generated values like ID</returns>
        [HttpPost]
        public ActionResult<User> AddUser(User.FullUser user)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                SVR_JMMUser jmmUser = user.GetServerModel();

                RepoFactory.JMMUser.Save(jmmUser);

                return new User(jmmUser);
            }
            catch (Exception e)
            {
                return InternalError(e.Message);
            }
        }

        /// <summary>
        /// Patch a User with JSON Patch.
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="user">JSON Patch document</param>
        /// <returns></returns>
        [HttpPatch("{id}")]
        public ActionResult PatchUser(int id, [FromBody] JsonPatchDocument<User> user)
        {
            if (user == null) return BadRequest("object is invalid.");
            var existing = RepoFactory.JMMUser.GetByID(id);
            if (existing == null) return BadRequest("No User with ID");
            var patchModel = new User(existing);
            user.ApplyTo(patchModel, ModelState);
            TryValidateModel(patchModel);
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            bool changedAdmin = existing.IsAdminUser() != patchModel.IsAdmin;
            if (changedAdmin)
            {
                var allAdmins = RepoFactory.JMMUser.GetAll().Where(a => a.IsAdminUser()).ToList();
                allAdmins.Remove(existing);
                if (allAdmins.Count < 1) return BadRequest("There must be at least one admin user");
            }

            var serverModel = patchModel.MergeServerModel(existing);
            RepoFactory.JMMUser.Save(serverModel);
            return Ok();
        }

        /// <summary>
        /// Edit User. This replaces all values, except Plex and Password. 
        /// </summary>
        /// <returns>APIStatus</returns>
        [HttpPut]
        public ActionResult EditUser(User user)
        {
            if (user.ID == 0)
                return BadRequest("ID missing. If this is a new User, then use POST");

            try
            {
                var existing = RepoFactory.JMMUser.GetByID(user.ID);
                if (existing == null) return BadRequest("User not found. If this is a new User, then use POST");
                bool changedAdmin = existing.IsAdminUser() != user.IsAdmin;
                if (changedAdmin)
                {
                    var allAdmins = RepoFactory.JMMUser.GetAll().Where(a => a.IsAdminUser()).ToList();
                    allAdmins.Remove(existing);
                    if (allAdmins.Count < 1) return BadRequest("There must be at least one admin user");
                }
                var newUser = user.MergeServerModel(existing);
                RepoFactory.JMMUser.Save(newUser);
                return Ok();
            }
            catch (Exception e)
            {
                return InternalError(e.Message);
            }
        }
        
        /// <summary>
        /// Change the Password to the new password. Can only be called by admins or the user the password belongs to
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="password"></param>
        /// <param name="revokeAPIKeys"></param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("ChangePassword/{userID}")]
        public ActionResult ChangePassword(int userID, [FromBody] string password, bool revokeAPIKeys = true)
        {
            try
            {
                SVR_JMMUser jmmUser = RepoFactory.JMMUser.GetByID(userID);
                if (jmmUser == null) return BadRequest("User not found");
                if (jmmUser.JMMUserID != User.JMMUserID && !User.IsAdminUser()) return Unauthorized();

                jmmUser.Password = Digest.Hash(password);
                RepoFactory.JMMUser.Save(jmmUser, false);
                if (revokeAPIKeys)
                {
                    RepoFactory.AuthTokens.DeleteAllWithUserID(jmmUser.JMMUserID);
                }
            }
            catch (Exception ex)
            {
                return InternalError(ex.ToString());
            }

            return Ok();
        }

        /// <summary>
        /// Delete a User. This updates group filters and wipes internal watched states, so be careful.
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public ActionResult DeleteUser(int id)
        {
            if (id == 0) return BadRequest("ID missing");
            var user = RepoFactory.JMMUser.GetByID(id);
            var allAdmins = RepoFactory.JMMUser.GetAll().Where(a => a.IsAdminUser()).ToList();
            allAdmins.Remove(user);
            if (allAdmins.Count < 1) return BadRequest("There must be at least one admin user");
            
            RepoFactory.JMMUser.RemoveUser(id, true);
            return Ok();
        }
    }
}