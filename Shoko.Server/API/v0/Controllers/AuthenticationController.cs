using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v0.Models;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v0.Controllers;

[ApiController]
[Route("/api/auth")]
[ApiVersionNeutral]
[AdvertiseApiVersions("2.0", "2.1", "3")]
public class AuthenticationController(
    IUserService userService,
    AuthTokensRepository authTokensRepository,
    ISettingsProvider settingsProvider
) : BaseController(settingsProvider)
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
            return userService.GetApiTokensForUser(User)
                .Select(token => new ApikeyResult(token.User.ID, token.User.Username, token.Device, token.ExpiresAt))
                .ToList();

        return userService.GetUsers()
            .SelectMany(user => userService.GetApiTokensForUser(user))
            .Select(token => new ApikeyResult(token.User.ID, token.User.Username, token.Device, token.ExpiresAt))
            .OrderBy(key => key.UserID)
            .ThenBy(key => key.Device)
            .ThenBy(key => key.ExpiresAt ?? DateTime.MaxValue)
            .ToList();
    }

    /// <summary>
    /// Creates a new apikey for the current user with a given "device"
    /// </summary>
    /// <param name="device">The device name</param>
    /// <returns>The new apikey</returns>
    [Obsolete("Use GenerateToken instead")]
    [HttpPost("apikey")]
    [Authorize]
    public async Task<ActionResult<string>> GenerateApikey([FromBody] string device)
    {
        if (string.IsNullOrWhiteSpace(device))
            return BadRequest("device cannot be empty");

        var currentExpires = GetCurrentTokenExpiresAt();
        if (currentExpires.HasValue)
            return BadRequest("Cannot create a non-expiring token from an expiring session.");

        var token = await userService.GenerateApiTokenForUser(User, device.Trim());
        return token.Token;
    }

    /// <summary>
    /// Get an authentication token for the user.
    /// </summary>
    /// <param name="request">The authentication details for the user.</param>
    /// <returns>HTTP 400, 401 or 200 with an APIKey response.</returns>
    [Obsolete("Use SignIn instead")]
    [HttpPost]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(200)]
    public async Task<ActionResult<object>> Login([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AuthUser request)
    {
        if (userService.AuthenticateUser(request.user.Trim(), request.pass) is not { } user)
            return Unauthorized();

        var expiresAt = ParseExpires(request.expires);
        if (expiresAt is null && request.expires is not null)
        {
            ModelState.AddModelError(nameof(request.expires), "Invalid expires value");
            return ValidationProblem(ModelState);
        }

        var token = expiresAt.HasValue
            ? await userService.GenerateApiTokenForUser(user, request.device.Trim(), expiresAt.Value)
            : await userService.GenerateApiTokenForUser(user, request.device.Trim());
        return Ok(new { apikey = token.Token });
    }

    /// <summary>
    /// Change the password. Invalidates the current user's apikeys. Reauth after using this!
    /// </summary>
    /// <param name="newPassword"></param>
    /// <param name="userID">Optionally, an admin can change another user's password</param>
    /// <returns></returns>
    [ApiV3]
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

    /// <summary>
    /// Signs in a user and returns an apikey.
    /// </summary>
    /// <param name="request">The authentication details for the user.</param>
    /// <returns>HTTP 400, 401 or 200 with an APIKey response.</returns>
    [ApiV3]
    [HttpPost("SignIn")]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(200)]
    public async Task<ActionResult<ApikeyResult>> SignIn([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AuthUser request)
    {
        if (userService.AuthenticateUser(request.user.Trim(), request.pass) is not { } user)
            return Unauthorized();

        var expiresAt = ParseExpires(request.expires);
        if (expiresAt is null && request.expires is not null)
        {
            ModelState.AddModelError(nameof(request.expires), "Invalid expires value");
            return ValidationProblem(ModelState);
        }

        var token = expiresAt.HasValue
            ? await userService.GenerateApiTokenForUser(user, request.device.Trim(), expiresAt.Value)
            : await userService.GenerateApiTokenForUser(user, request.device.Trim());
        return Ok(new ApikeyResult(user.ID, user.Username, token.Device, token.ExpiresAt));
    }

    /// <summary>
    ///   Generates a new API token for the signed-in user.
    /// </summary>
    /// <param name="request">
    ///   The authentication details for the user.
    /// </param>
    /// <returns>
    ///   HTTP 400, 401 or 200 with an APIKey response.
    /// </returns>
    [ApiV3]
    [Authorize]
    [HttpPost("GenerateToken")]
    [ProducesResponseType(400)]
    public async Task<ActionResult<ApikeyResult>> GenerateToken([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AuthApikeyRequest request)
    {
        var expiresAt = ParseExpires(request.expires);
        if (expiresAt is null && request.expires is not null)
        {
            ModelState.AddModelError(nameof(request.expires), "Invalid expires value");
            return ValidationProblem(ModelState);
        }

        var currentExpires = GetCurrentTokenExpiresAt();
        if (currentExpires.HasValue)
        {
            if (!expiresAt.HasValue)
                return BadRequest("Cannot create a non-expiring token from an expiring session.");

            if (expiresAt.Value > currentExpires.Value)
                return BadRequest("Requested expiration extends beyond the current session's expiration.");
        }

        var token = expiresAt.HasValue
            ? await userService.GenerateApiTokenForUser(User, request.device.Trim(), expiresAt.Value)
            : await userService.GenerateApiTokenForUser(User, request.device.Trim());
        return Ok(new ApikeyResult(token.User.ID, token.User.Username, token.Device, token.ExpiresAt));
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
        if (User is IUser { } user && await userService.InvalidateApiDeviceForUser(user, apikey))
            return Ok();

        // Attempt to invalidate apikey.
        if (await userService.InvalidateApiToken(apikey))
            return Ok();

        return BadRequest("Could not find apikey or device name to delete");
    }

    private DateTime? GetCurrentTokenExpiresAt()
    {
        var (tokenValue, _) = HttpContext.GetToken();
        if (tokenValue is null)
            return null;

        var authToken = authTokensRepository.GetByToken(tokenValue);
        return authToken?.ExpiresAt;
    }

    private static DateTime? ParseExpires(string expires)
    {
        if (string.IsNullOrWhiteSpace(expires))
            return null;

        // Try simple duration: "7d", "24h", "30m"
        if (expires.Length > 1 && char.IsDigit(expires[0]) && char.IsLetter(expires[^1]))
        {
            var numPart = expires[..^1];
            var unit = expires[^1];
            if (int.TryParse(numPart, out var amount) && amount > 0)
            {
                var span = unit switch
                {
                    'm' => TimeSpan.FromMinutes(amount),
                    'h' => TimeSpan.FromHours(amount),
                    'd' => TimeSpan.FromDays(amount),
                    _ => (TimeSpan?)null,
                };
                if (span.HasValue)
                    return DateTime.Now.Add(span.Value);
            }
        }

        // Try ISO 8601 duration: "P7D", "PT24H"
        try
        {
            var isoSpan = System.Xml.XmlConvert.ToTimeSpan(expires);
            return DateTime.Now.Add(isoSpan);
        }
        catch (FormatException) { }

        // Try datetime
        if (DateTime.TryParse(expires, out var dt))
            return dt;

        return null;
    }
}
