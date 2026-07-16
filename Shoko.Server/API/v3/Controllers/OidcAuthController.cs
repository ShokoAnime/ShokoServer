using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API;
using Shoko.Server.API.Annotations;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Optional OpenID Connect single sign-on. Disabled unless configured in
/// Settings.Oidc. Never creates or auto-matches local accounts by username
/// or email — an already-authenticated user must explicitly link their
/// account via <see cref="Link"/>. Sign-in only succeeds for a subject that
/// has already been linked.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/Auth/Oidc")]
[ApiV3]
public class OidcAuthController(
    ISettingsProvider settingsProvider,
    JMMUserRepository userRepository,
    IUserService userService,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtectionProvider
) : ControllerBase
{
    private const string ProtectorPurpose = "Shoko.Server.OidcAuth.State";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    // Override Controller.User to be the JMMUser, matching BaseController's convention.
    protected new JMMUser? User => HttpContext.User.Identity?.IsAuthenticated == true ? HttpContext.GetUser() : null;

    private OidcSettings Settings => settingsProvider.GetSettings().Oidc;

    // LinkUserID is only ever populated by the authenticated Link endpoint, never
    // attacker-controlled — the payload is signed/encrypted by IDataProtector.
    private sealed record StatePayload(string Nonce, string? ReturnUrl, DateTime CreatedAt, int? LinkUserID = null);

    /// <summary>
    /// Redirects the browser to the configured OIDC provider's authorization
    /// endpoint.
    /// </summary>
    [HttpGet("Challenge")]
    [AllowAnonymous]
    public async Task<ActionResult> Challenge(
        [FromQuery] string? returnUrl = null,
        [FromHeader(Name = "X-Forwarded-Proto")] string? forwardedProto = null)
        => await StartAuthorizeAsync(returnUrl, linkUserID: null, forwardedProto);

    /// <summary>
    /// Starts the OIDC flow to link the currently signed-in local account to
    /// an external identity. Unlike <see cref="Challenge"/>, this requires an
    /// authenticated session and never signs in as a different user — the
    /// callback only ever links to the account that started this flow.
    /// </summary>
    [HttpGet("Link")]
    [Authorize]
    public async Task<ActionResult> Link(
        [FromQuery] string? returnUrl = null,
        [FromHeader(Name = "X-Forwarded-Proto")] string? forwardedProto = null)
        => await StartAuthorizeAsync(returnUrl, linkUserID: User!.JMMUserID, forwardedProto);

    /// <summary>
    /// Removes the external identity link from the currently signed-in
    /// local account, if any.
    /// </summary>
    [HttpPost("Unlink")]
    [Authorize]
    public ActionResult Unlink()
    {
        var user = User!;
        if (user.ExternalAuthID is null)
            return NoContent();

        user.ExternalAuthID = null;
        userRepository.Save(user);
        return NoContent();
    }

    private async Task<ActionResult> StartAuthorizeAsync(string? returnUrl, int? linkUserID, string? forwardedProto)
    {
        var settings = Settings;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Authority) || string.IsNullOrWhiteSpace(settings.ClientID))
            return NotFound("OIDC sign-in is not enabled.");

        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await GetProviderConfigurationAsync(settings.Authority);
        }
        catch (Exception)
        {
            return RedirectToWebUiWithError("Could not reach the OIDC provider. Please try again later.");
        }

        var nonce = Guid.NewGuid().ToString("N");
        var state = ProtectState(new StatePayload(nonce, returnUrl, DateTime.UtcNow, linkUserID));

        var authorizeUrl = QueryHelpers.AddQueryString(configuration.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = settings.ClientID,
            ["response_type"] = "code",
            ["scope"] = "openid profile email",
            ["redirect_uri"] = BuildRedirectUri(forwardedProto),
            ["state"] = state,
            ["nonce"] = nonce,
        });

        return Redirect(authorizeUrl);
    }

    /// <summary>
    /// Handles the redirect back from the OIDC provider, exchanges the code
    /// for tokens, validates the ID token, and links/signs in the matching
    /// local user.
    /// </summary>
    [HttpGet("Callback")]
    [AllowAnonymous]
    public async Task<ActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromHeader(Name = "X-Forwarded-Proto")] string? forwardedProto = null)
    {
        var settings = Settings;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Authority) || string.IsNullOrWhiteSpace(settings.ClientID))
            return NotFound("OIDC sign-in is not enabled.");

        if (!string.IsNullOrEmpty(error))
            return RedirectToWebUiWithError($"OIDC provider returned an error: {error}");

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest("Missing code or state.");

        if (UnprotectState(state) is not { } statePayload || DateTime.UtcNow - statePayload.CreatedAt > StateLifetime)
            return BadRequest("Invalid or expired sign-in attempt. Please try again.");

        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await GetProviderConfigurationAsync(settings.Authority);
        }
        catch (Exception)
        {
            return RedirectToWebUiWithError("Could not reach the OIDC provider. Please try again later.");
        }

        var (idToken, exchangeError) = await ExchangeCodeForIdTokenAsync(configuration, settings, code, forwardedProto);
        if (exchangeError is not null)
            return RedirectToWebUiWithError(exchangeError);

        var (claims, validationError) = await ValidateIdTokenAsync(configuration, settings, idToken!, statePayload.Nonce);
        if (validationError is not null)
            return RedirectToWebUiWithError(validationError);

        var rawSubject = claims!.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(rawSubject))
            return RedirectToWebUiWithError("ID token is missing a subject claim.");

        // Prefix with the authority so switching Settings.Oidc.Authority can never make an
        // identity minted by a different provider resolve to the same external auth ID.
        var externalAuthID = $"{settings.Authority}::{rawSubject}";

        var (user, userError) = statePayload.LinkUserID is { } linkUserID
            ? LinkUser(linkUserID, externalAuthID)
            : ResolveUser(externalAuthID);
        if (userError is not null)
            return RedirectToWebUiWithError(userError);

        // Match the Shoko token's lifetime to the OIDC token's own expiry rather than a
        // fixed value — a fresh login always mints a new token, so several can coexist
        // with different expirations without any one of them ever being non-expiring.
        if (GetExpiration(claims!) is not { } expiresAt || expiresAt <= DateTime.UtcNow.AddMinutes(1))
            return RedirectToWebUiWithError("ID token is missing a valid expiration.");

        var apiToken = await userService.GenerateApiTokenForUser(user!, $"OIDC — {settings.Authority}", expiresAt);
        return RedirectToWebUiWithToken(apiToken.Token, statePayload.ReturnUrl);
    }

    private async Task<(string? IdToken, string? Error)> ExchangeCodeForIdTokenAsync(OpenIdConnectConfiguration configuration, OidcSettings settings, string code, string? forwardedProto)
    {
        var client = httpClientFactory.CreateClient("Default");
        using var tokenResponse = await client.PostAsync(configuration.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = BuildRedirectUri(forwardedProto),
            ["client_id"] = settings.ClientID!,
            ["client_secret"] = settings.ClientSecret ?? string.Empty,
        }));

        if (!tokenResponse.IsSuccessStatusCode)
            return (null, "Failed to exchange authorization code with the OIDC provider.");

        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        if (!tokenPayload.TryGetProperty("id_token", out var idTokenElement))
            return (null, "OIDC provider did not return an ID token.");

        var idToken = idTokenElement.GetString();
        return string.IsNullOrEmpty(idToken) ? (null, "OIDC provider returned an empty ID token.") : (idToken, null);
    }

    private static async Task<(ClaimsIdentity? Claims, string? Error)> ValidateIdTokenAsync(OpenIdConnectConfiguration configuration, OidcSettings settings, string idToken, string expectedNonce)
    {
        var handler = new JsonWebTokenHandler();
        var validationResult = await handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = configuration.Issuer,
            ValidAudience = settings.ClientID,
            IssuerSigningKeys = configuration.SigningKeys,
        });

        if (!validationResult.IsValid)
            return (null, "ID token validation failed.");

        var claims = validationResult.ClaimsIdentity;
        var tokenNonce = claims.FindFirst("nonce")?.Value;
        if (tokenNonce is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(tokenNonce), Encoding.UTF8.GetBytes(expectedNonce)))
            return (null, "ID token nonce mismatch.");

        return (claims, null);
    }

    private static DateTime? GetExpiration(ClaimsIdentity claims)
        => claims.FindFirst("exp")?.Value is { } expClaim && long.TryParse(expClaim, out var expSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime
            : null;

    // Sign-in only ever resolves a user that was already explicitly linked via Link() —
    // never matches by username or email, which would let anyone controlling (or
    // impersonating) the IdP take over a same-named local account.
    private (JMMUser? User, string? Error) ResolveUser(string externalAuthID)
    {
        var user = userRepository.GetByExternalAuthID(externalAuthID);
        return user is not null
            ? (user, null)
            : (null, "No local Shoko account is linked to this SSO identity. Sign in locally and link your account first.");
    }

    private (JMMUser? User, string? Error) LinkUser(int linkUserID, string externalAuthID)
    {
        var user = userRepository.GetByID(linkUserID);
        if (user is null)
            return (null, "The account that started this link no longer exists.");

        var existingLink = userRepository.GetByExternalAuthID(externalAuthID);
        if (existingLink is not null && existingLink.JMMUserID != user.JMMUserID)
            return (null, "This SSO identity is already linked to a different local account.");

        if (user.ExternalAuthID is not null && user.ExternalAuthID != externalAuthID)
            return (null, "This local account is already linked to a different SSO identity. Unlink it first.");

        user.ExternalAuthID = externalAuthID;
        userRepository.Save(user);
        return (user, null);
    }

    private static async Task<OpenIdConnectConfiguration> GetProviderConfigurationAsync(string authority)
    {
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
            authority.TrimEnd('/') + "/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());
        return await manager.GetConfigurationAsync();
    }

    private string BuildRedirectUri(string? forwardedProto)
    {
        // Request.Scheme reflects the connection Kestrel actually accepted, which is
        // http when Shoko sits behind a TLS-terminating reverse proxy. Honor
        // X-Forwarded-Proto so the redirect_uri we send matches what's registered
        // with the OIDC provider.
        var scheme = !string.IsNullOrWhiteSpace(forwardedProto)
            ? forwardedProto.Split(',')[0].Trim()
            : Request.Scheme;
        return $"{scheme}://{Request.Host}/api/v3/Auth/Oidc/Callback";
    }

    private string ProtectState(StatePayload payload)
        => dataProtectionProvider.CreateProtector(ProtectorPurpose).Protect(JsonSerializer.Serialize(payload));

    private StatePayload? UnprotectState(string state)
    {
        try
        {
            return JsonSerializer.Deserialize<StatePayload>(dataProtectionProvider.CreateProtector(ProtectorPurpose).Unprotect(state));
        }
        catch
        {
            return null;
        }
    }

    private ActionResult RedirectToWebUiWithToken(string token, string? returnUrl)
    {
        returnUrl ??= settingsProvider.GetSettings().Web.WebUIPublicPath;
        return Redirect($"{returnUrl}#oidcToken={Uri.EscapeDataString(token)}");
    }

    private ActionResult RedirectToWebUiWithError(string message)
    {
        var returnUrl = settingsProvider.GetSettings().Web.WebUIPublicPath;
        return Redirect($"{returnUrl}#oidcError={Uri.EscapeDataString(message)}");
    }
}
