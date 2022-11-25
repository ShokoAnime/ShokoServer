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

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class UserController : BaseController
{
    /// <summary>
    /// List all Users. Admin only
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet]
    public ActionResult<List<User>> GetUsers()
    {
        return RepoFactory.JMMUser.GetAll().Select(a => new User(a)).ToList();
    }

    /// <summary>
    /// Add a User. Admin only
    /// </summary>
    /// <returns>User with generated values like ID</returns>
    [Authorize("admin")]
    [HttpPost]
    public ActionResult<User> AddUser(User.FullUser user)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (user.ID != 0)
        {
            return BadRequest("User ID must be 0 when adding a new user.");
        }

        var jmmUser = user.GetServerModel();

        RepoFactory.JMMUser.Save(jmmUser);

        return new User(jmmUser);
    }

    /// <summary>
    /// Patch a User with JSON Patch.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <param name="patch">JSON Patch document</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPatch("{userID}")]
    public ActionResult PatchUser([FromRoute] int userID, [FromBody] JsonPatchDocument<User> patch)
    {
        if (patch == null)
        {
            return BadRequest("object is invalid.");
        }

        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        var patchModel = new User(user);
        patch.ApplyTo(patchModel, ModelState);
        TryValidateModel(patchModel);
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var changedAdmin = user.IsAdminUser() != patchModel.IsAdmin;
        if (changedAdmin)
        {
            var allAdmins = RepoFactory.JMMUser.GetAll().Where(a => a.IsAdminUser()).ToList();
            allAdmins.Remove(user);
            if (allAdmins.Count < 1)
            {
                return BadRequest("There must be at least one admin user.");
            }
        }

        var serverModel = patchModel.MergeServerModel(user);
        RepoFactory.JMMUser.Save(serverModel);

        return Ok();
    }

    /// <summary>
    /// Edit User. This replaces all values, except Plex and Password. 
    /// </summary>
    /// <returns>APIStatus</returns>
    [Authorize("admin")]
    [HttpPut]
    public ActionResult EditUser([FromBody] User user)
    {
        if (user.ID == 0)
        {
            return BadRequest("User ID is missing. If this is a new user then use POST.");
        }

        var existing = RepoFactory.JMMUser.GetByID(user.ID);
        if (existing == null)
        {
            return NotFound("User not found.");
        }

        var changedAdmin = existing.IsAdminUser() != user.IsAdmin;
        if (changedAdmin)
        {
            var allAdmins = RepoFactory.JMMUser.GetAll().Where(a => a.IsAdminUser()).ToList();
            allAdmins.Remove(existing);
            if (allAdmins.Count < 1)
            {
                return BadRequest("There must be at least one admin user.");
            }
        }

        var newUser = user.MergeServerModel(existing);
        RepoFactory.JMMUser.Save(newUser);

        return Ok();
    }

    /// <summary>
    /// Get the current <see cref="User"/>.
    /// </summary>
    /// <returns>The user.</returns>
    [HttpGet("Current")]
    public ActionResult<User> GetCurrentUser()
    {
        return new User(User);
    }

    /// <summary>
    /// Change the password for the current <see cref="User"/>.
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    [HttpPost("Current/ChangePassword")]
    public ActionResult ChangePasswordForCurrentUser([FromBody] User.Input.ChangePasswordBody body)
    {
        return ChangePassword(User, body);
    }

    /// <summary>
    /// Get a user by id.
    /// </summary>
    /// <param name="userID"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("{userID}")]
    public ActionResult<User> GetUserByUserID([FromRoute] int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        return new User(user);
    }

    /// <summary>
    /// Change the password for a user. Can only be called by admins or the user the password belongs to.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <param name="body">The change password request body.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{userID}/ChangePassword")]
    public ActionResult ChangePasswordForUserByUserID([FromRoute] int userID,
        [FromBody] User.Input.ChangePasswordBody body)
    {
        return ChangePassword(RepoFactory.JMMUser.GetByID(userID), body);
    }

    [NonAction]
    private ActionResult ChangePassword(SVR_JMMUser user, User.Input.ChangePasswordBody body)
    {
        if (user == null)
        {
            return NotFound("User not found.");
        }

        if (user.JMMUserID != User.JMMUserID && !User.IsAdminUser())
        {
            return Forbid("User must be admin to change other's password.");
        }

        user.Password = Digest.Hash(body.Password);
        RepoFactory.JMMUser.Save(user, false);
        if (body.RevokeAPIKeys)
        {
            RepoFactory.AuthTokens.DeleteAllWithUserID(user.JMMUserID);
        }

        return Ok();
    }

    /// <summary>
    /// Delete a User. This updates group filters and wipes internal watched states, so be careful.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{userID}")]
    public ActionResult DeleteUser([FromRoute] int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);

        if (user == null)
        {
            return NotFound("User not found.");
        }

        var allAdmins = RepoFactory.JMMUser.GetAll().Where(a => a.IsAdminUser()).ToList();
        allAdmins.Remove(user);
        if (allAdmins.Count < 1)
        {
            return BadRequest("There must be at least one admin user.");
        }

        RepoFactory.JMMUser.RemoveUser(userID, true);
        return Ok();
    }
}
