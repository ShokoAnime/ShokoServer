using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class UserController : BaseController
{
    private const string UserByIdNotFound = "An user by the given `userID` doesn't exist.";

    /// <summary>
    /// List all available users.
    /// </summary>
    /// <remarks>
    /// Only for administrators.
    /// </remarks>
    /// <returns>The users.</returns>
    [Authorize("admin")]
    [HttpGet]
    public ActionResult<List<User>> GetUsers() =>
        RepoFactory.JMMUser.GetAll().Select(user => new User(user)).ToList();

    /// <summary>
    /// Add a new user.
    /// </summary>
    /// <remarks>
    /// Only for administrators.
    /// </remarks>
    /// <returns>User with generated values like ID</returns>
    [Authorize("admin")]
    [HttpPost]
    public ActionResult<User> AddNewUser(User.Input.CreateUserBody body)
    {
        var user = body.Save(ModelState, true);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return user;
    }

    /// <summary>
    /// Get the current user.
    /// </summary>
    /// <returns>The user.</returns>
    [HttpGet("Current")]
    public ActionResult<User> GetCurrentUser() =>
        new User(User);

    /// <summary>
    /// Edit the current user using a JSON patch document to do a partial
    /// update.
    /// </summary>
    /// <param name="document">JSON patch document for the partial update.</param>
    /// <returns>The updated current user.</returns>
    [HttpPatch("Current")]
    public ActionResult<User> PatchCurrentUser([FromBody] JsonPatchDocument<User.Input.CreateOrUpdateUserBody> document)
    {
        var user = User;
        var body = new User.Input.CreateOrUpdateUserBody();
        document.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = body.MergeWithExisting(user, ModelState, user.IsAdmin == 1);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return result;
    }

    /// <summary>
    /// Edit the current user using a raw object to do a partial update.
    /// </summary>
    /// <param name="body">The partial document for the changes to be made to
    /// the user.</param>
    /// <returns>The updated current user.</returns>
    [HttpPut("Current")]
    public ActionResult<User> PutCurrentUser([FromBody] User.Input.CreateOrUpdateUserBody body)
    {
        var user = User;
        var result = body.MergeWithExisting(user, ModelState, user.IsAdmin == 1);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return result;
    }

    /// <summary>
    /// Change the password for the current user.
    /// </summary>
    /// <param name="body">The body with the new password.</param>
    /// <returns></returns>
    [HttpPost("Current/ChangePassword")]
    public ActionResult ChangePasswordForCurrentUser([FromBody] User.Input.ChangePasswordBody body) =>
        ChangePassword(User, body);

    /// <summary>
    /// Get a user by id.
    /// </summary>
    /// <remarks>
    /// Only for administrators.
    /// </remarks>
    /// <param name="userID">User ID</param>
    /// <returns>The user.</returns>
    [Authorize("admin")]
    [HttpGet("{userID}")]
    public ActionResult<User> GetUserByUserID([FromRoute, Range(1, int.MaxValue)] int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound(UserByIdNotFound);

        return new User(user);
    }

    /// <summary>
    /// Edit a user by id using a JSON patch document to do a partial update.
    /// </summary>
    /// <remarks>
    /// Only for administrators.
    /// </remarks>
    /// <param name="userID">User ID</param>
    /// <param name="document">JSON patch document for the partial update.</param>
    /// <returns>The updated user.</returns>
    [Authorize("admin")]
    [HttpPatch("{userID}")]
    public ActionResult<User> PatchUserByUserID([FromRoute, Range(1, int.MaxValue)] int userID, [FromBody] JsonPatchDocument<User.Input.CreateOrUpdateUserBody> document)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound(UserByIdNotFound);

        var body = new User.Input.CreateOrUpdateUserBody();
        document.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = body.MergeWithExisting(user, ModelState, true);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return result;
    }

    /// <summary>
    /// Edit a user by id using a raw object to do a partial update.
    /// </summary>
    /// <remarks>
    /// Only for administrators.
    /// </remarks>
    /// <param name="userID">User ID</param>
    /// <param name="body">The partial document for the changes to be made to
    /// the user.</param>
    /// <returns>The updated user.</returns>
    [Authorize("admin")]
    [HttpPut("{userID}")]
    public ActionResult<User> PutUserByUserID([FromRoute, Range(1, int.MaxValue)] int userID, [FromBody] User.Input.CreateOrUpdateUserBody body)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound(UserByIdNotFound);

        var result = body.MergeWithExisting(user, ModelState, true);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return result;
    }

    /// <summary>
    /// Delete a user by id. This updates group filters and wipes internal watched states, so be careful.
    /// </summary>
    /// <remarks>
    /// Only for administrators.
    /// </remarks>
    /// <param name="userID">User ID</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpDelete("{userID}")]
    public ActionResult DeleteUser([FromRoute, Range(1, int.MaxValue)] int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return NotFound(UserByIdNotFound);

        var allAdmins = RepoFactory.JMMUser.GetAll().Where(a => a.IsAdminUser()).ToList();
        allAdmins.Remove(user);
        if (allAdmins.Count < 1)
            return ValidationProblem("There must be at least one admin user.", "IsAdmin");

        RepoFactory.JMMUser.RemoveUser(userID, true);
        return Ok();
    }

    /// <summary>
    /// Change the password for a user.
    /// </summary>
    /// <remarks>
    /// Can only be called by admins or the user the password belongs to.
    /// </remarks>
    /// <param name="userID">User ID</param>
    /// <param name="body">The change password request body.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{userID}/ChangePassword")]
    public ActionResult ChangePasswordForUserByUserID([FromRoute, Range(1, int.MaxValue)] int userID, [FromBody] User.Input.ChangePasswordBody body) =>
        ChangePassword(RepoFactory.JMMUser.GetByID(userID), body);

    [NonAction]
    private ActionResult ChangePassword(SVR_JMMUser user, User.Input.ChangePasswordBody body)
    {
        if (user == null)
            return NotFound(UserByIdNotFound);

        if (user.JMMUserID != User.JMMUserID && !User.IsAdminUser())
            return Forbid("User must be admin to change other's password.");

        user.Password = string.IsNullOrEmpty(body.Password) ? "" : Digest.Hash(body.Password);
        RepoFactory.JMMUser.Save(user);
        if (body.RevokeAPIKeys)
            RepoFactory.AuthTokens.DeleteAllWithUserID(user.JMMUserID);

        return Ok();
    }

    public UserController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
