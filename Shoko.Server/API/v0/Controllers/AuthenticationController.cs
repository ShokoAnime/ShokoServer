using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v0.Controllers;

[ApiController]
[Route("/api/auth")]
[ApiVersionNeutral]
[AdvertiseApiVersions("2.0", "2.1", "3")]
public class AuthenticationController : BaseController
{
    private readonly ILogger<AuthenticationController> _logger;

    /// <summary>
    /// Lists all apikeys and the user that they are associated with.
    /// Admins can list all apikeys. Otherwise, the current user's apikeys are listed.
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Authorize]
    public ActionResult<List<ApikeyResult>> GetApikeys()
    {
        // Only admins can list all apikeys, otherwise, just the current user
        if (User.IsAdmin == 0)
            return RepoFactory.AuthTokens.GetAll().Where(a => a.UserID == User.JMMUserID).Select(a => new ApikeyResult(a.UserID, User.Username, a.DeviceName))
                .ToList();

        var users = RepoFactory.JMMUser.GetAll().ToDictionary(a => a.JMMUserID, a => a.Username);
        return RepoFactory.AuthTokens.GetAll().Select(a => new ApikeyResult(a.UserID, users.TryGetValue(a.UserID, out var user) ? user : null, a.DeviceName))
            .ToList();
    }

    /// <summary>
    /// Creates a new apikey for the current user with a given "device"
    /// </summary>
    /// <param name="device">A unique identifier for this apikey. Can be anything, but creating a new key with the same device will invalidate previous keys</param>
    /// <returns>The new apikey</returns>
    [HttpPost("apikey")]
    [Authorize]
    public ActionResult<string> GenerateApikey([FromBody] string device)
    {
        if (string.IsNullOrWhiteSpace(device)) return BadRequest("device cannot be empty");
        return RepoFactory.AuthTokens.CreateNewApiKey(User, device);
    }

    /// <summary>
    /// Get an authentication token for the user.
    /// </summary>
    /// <param name="auth">The authentication details for the user.</param>
    /// <returns>HTTP 400, 401 or 200 with an APIKey response.</returns>
    [HttpPost]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(200)]
    public ActionResult<object> Login(AuthUser auth)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(auth.user?.Trim())) return BadRequest(ModelState);
        auth.pass ??= string.Empty;

        //create and save new token for authenticated user or return known one
        var apiKey = RepoFactory.AuthTokens.ValidateUser(auth.user.Trim(), auth.pass.Trim(), auth.device.Trim());

        if (!string.IsNullOrEmpty(apiKey)) return Ok(new { apikey = apiKey });
        return Unauthorized();
    }

    /// <summary>
    /// Change the password. Invalidates the current user's apikeys. Reauth after using this!
    /// </summary>
    /// <param name="newPassword"></param>
    /// <param name="userID">Optionally, an admin can change another user's password</param>
    /// <returns></returns>
    [HttpPost("ChangePassword")]
    [Authorize]
    public ActionResult ChangePassword([FromBody] string newPassword, [FromQuery] int? userID = null)
    {
        try
        {
            var user = User;
            if (userID != null && User.IsAdmin == 1) user = RepoFactory.JMMUser.GetByID(userID.Value);
            if (user == null) return BadRequest("Could not get user");
            user.Password = Digest.Hash(newPassword.Trim());
            RepoFactory.JMMUser.Save(user);
            RepoFactory.AuthTokens.DeleteAllWithUserID(user.JMMUserID);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.ToString());
        }

        return InternalError();
    }

    ///<summary>
    ///Delete an APIKey from the database.
    ///</summary>
    ///<param name="apikey">The Apikey or device to delete.</param>
    [HttpDelete]
    public ActionResult Delete([FromBody] string apikey)
    {
        if (apikey == null) return BadRequest("Must provide an apikey or device name to delete");
        var token = RepoFactory.AuthTokens.GetAll().FirstOrDefault(a => a.UserID == User?.JMMUserID && apikey.EqualsInvariantIgnoreCase(a.DeviceName));
        token ??= RepoFactory.AuthTokens.GetByToken(apikey);
        if (token == null) return BadRequest("Could not find apikey or device name to delete");
        RepoFactory.AuthTokens.Delete(token);
        return Ok();
    }

    public AuthenticationController(ISettingsProvider settingsProvider, ILogger<AuthenticationController> logger) : base(settingsProvider)
    {
        _logger = logger;
    }

    public record ApikeyResult(int UserID, string Username, string Device);
}
