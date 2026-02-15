using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.User;
using Shoko.Server.API.v0.Models;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v0.Controllers;

[ApiController]
[Route("/api/auth")]
[ApiVersionNeutral]
[AdvertiseApiVersions("2.0", "2.1", "3")]
public class AuthenticationController(IUserService userService, ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
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
            return userService.ListRestApiDevicesForUser(User)
                .Select(deviceName => new ApikeyResult(User.JMMUserID, User.Username, deviceName))
                .ToList();

        return userService.GetUsers()
            .SelectMany(user => userService.ListRestApiDevicesForUser(user)
                .Select(deviceName => new ApikeyResult(user.ID, user.Username, deviceName))
            )
            .OrderBy(apiKey => apiKey.UserID)
            .ThenBy(apiKey => apiKey.Device)
            .ToList();
    }

    /// <summary>
    /// Creates a new apikey for the current user with a given "device"
    /// </summary>
    /// <param name="device">A unique identifier for this apikey. Can be anything, but creating a new key with the same device will invalidate previous keys</param>
    /// <returns>The new apikey</returns>
    [HttpPost("apikey")]
    [Authorize]
    public async Task<ActionResult<string>> GenerateApikey([FromBody] string device)
    {
        if (string.IsNullOrWhiteSpace(device))
            return BadRequest("device cannot be empty");
        return await userService.GenerateRestApiTokenForUser(User, device);
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
    public async Task<ActionResult<object>> Login(AuthUser auth)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(auth.user?.Trim())) return BadRequest(ModelState);
        auth.pass ??= string.Empty;

        //create and save new token for authenticated user or return known one
        var user = userService.AuthenticateUser(auth.user.Trim(), auth.pass.Trim());
        if (user == null) return Unauthorized();
        var apiKey = await userService.GenerateRestApiTokenForUser(user, auth.device.Trim());

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
    public async Task<ActionResult> ChangePassword([FromBody] string newPassword, [FromQuery] int? userID = null)
    {
        var user = (IUser)User;
        if (userID.HasValue && user.IsAdmin)
            user = userService.GetUserByID(userID.Value);
        if (user == null)
            return BadRequest("Could not get user");

        await userService.ChangeUserPassword(user, newPassword);
        return Ok();
    }

    ///<summary>
    ///Delete an APIKey from the database.
    ///</summary>
    ///<param name="apikey">The Apikey or device to delete.</param>
    [HttpDelete]
    public async Task<ActionResult> Delete([FromBody] string apikey)
    {
        if (apikey is not { Length: > 0 })
            return BadRequest("Must provide an apikey or device name to delete");

        // Attempt to invalidate device name for user.
        if (User is IUser { } user && await userService.InvalidateRestApiDeviceForUser(user, apikey))
            return Ok();

        // Attempt to invalidate apikey.
        if (await userService.InvalidateRestApiToken(apikey))
            return Ok();

        return BadRequest("Could not find apikey or device name to delete");
    }
}
