using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Auth;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

using ApiToken = Shoko.Server.API.v3.Models.Auth.ApiToken;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for user authentication.
/// </summary>
/// <param name="userService"></param>
/// <param name="authTokensRepository"></param>
/// <param name="settingsProvider"></param>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
public class AuthController(
    IUserService userService,
    AuthTokensRepository authTokensRepository,
    ISettingsProvider settingsProvider
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Signs in the user and returns an API token.
    /// </summary>
    /// <param name="request">The authentication details for the user.</param>
    /// <returns>HTTP 400, 401 or 200 with an API token response.</returns>
    [HttpPost("SignIn")]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(200)]
    public async Task<ActionResult<ApiToken>> SignIn([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] SignInRequest request)
    {
        if (userService.AuthenticateUser(request.User.Trim(), request.Password) is not { } user)
            return Unauthorized();

        var expiresAt = ParseExpires(request.Expires);
        if (expiresAt is null && request.Expires is not null)
        {
            ModelState.AddModelError(nameof(request.Expires), "Invalid expires value");
            return ValidationProblem(ModelState);
        }

        var token = expiresAt.HasValue
            ? await userService.GenerateApiTokenForUser(user, request.Device.Trim(), expiresAt.Value)
            : await userService.GenerateApiTokenForUser(user, request.Device.Trim());
        return Ok(new ApiToken(user.ID, user.Username, token.Device, token.ExpiresAt, token.Token));
    }

    /// <summary>
    ///   Generates a new API token for the signed-in user.
    /// </summary>
    /// <param name="request">
    ///   The authentication details for the user.
    /// </param>
    /// <returns>
    ///   HTTP 400, 401 or 200 with an API token response.
    /// </returns>
    [Authorize]
    [HttpPost("GenerateToken")]
    [ProducesResponseType(400)]
    public async Task<ActionResult<ApiToken>> GenerateToken([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] GenerateTokenRequest request)
    {
        var expiresAt = ParseExpires(request.Expires);
        if (expiresAt is null && request.Expires is not null)
        {
            ModelState.AddModelError(nameof(request.Expires), "Invalid expires value");
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
            ? await userService.GenerateApiTokenForUser(User, request.Device.Trim(), expiresAt.Value)
            : await userService.GenerateApiTokenForUser(User, request.Device.Trim());
        return Ok(new ApiToken(token.User.ID, token.User.Username, token.Device, token.ExpiresAt, token.Token));
    }

    /// <summary>
    /// Lists all API tokens and the user that they are associated with.
    /// </summary>
    /// <param name="showAll">
    /// Optional. If true and the user is an admin; returns all API tokens for
    /// all users.
    /// </param>
    /// <returns></returns>
    [HttpGet("ListTokens")]
    [Authorize]
    public ActionResult<List<ApiToken>> GetApiTokens([FromQuery] bool showAll = false)
    {
        if (User.IsAdmin is 0 || !showAll)
            return userService.GetApiTokensForUser(User)
                .Select(token => new ApiToken(token.User.ID, token.User.Username, token.Device, token.ExpiresAt))
                .ToList();

        return userService.GetUsers()
            .SelectMany(userService.GetApiTokensForUser)
            .Select(token => new ApiToken(token.User.ID, token.User.Username, token.Device, token.ExpiresAt))
            .OrderBy(token => token.UserID)
            .ThenBy(token => token.Device)
            .ThenBy(token => token.ExpiresAt ?? DateTime.MaxValue)
            .ToList();
    }

    ///<summary>
    /// Invalidates an API token and removes it from the database.
    ///</summary>
    ///<param name="token">The token to invalidate.</param>
    [HttpDelete("InvalidateToken")]
    public async Task<ActionResult> Delete([FromQuery] string? token = null)
    {
        if (token is not { Length: > 0 } && User is not { })
            return BadRequest("An API token must be provided if not signed in.");

        // Attempt to invalidate token.
        token ??= HttpContext.GetToken();
        if (await userService.InvalidateApiToken(token))
            return Ok();

        return BadRequest("Unable to invalidate token.");
    }

    /// <summary>
    /// Change the password. Invalidates the current user's API tokens. Reauth after using this!
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
        if (user is null)
            return BadRequest("Unable to find user.");

        await userService.ChangeUserPassword(user, newPassword);
        return Ok();
    }

    private DateTime? GetCurrentTokenExpiresAt()
    {
        var tokenValue = HttpContext.GetToken();
        if (tokenValue is null)
            return null;

        var authToken = authTokensRepository.GetByToken(tokenValue);
        return authToken?.ExpiresAt;
    }

    private static DateTime? ParseExpires(string? expires)
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
