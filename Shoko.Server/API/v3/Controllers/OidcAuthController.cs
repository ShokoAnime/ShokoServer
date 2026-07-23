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
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.User.Update;
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
    AuthTokensRepository authTokensRepository,
    IUserService userService,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtectionProvider
) : BaseController(settingsProvider)
{
    private const string ProtectorPurpose = "Shoko.Server.OidcAuth.State";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

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
        => await StartAuthorizeAsync(returnUrl, linkUserID: User.JMMUserID, forwardedProto);

    /// <summary>
    /// Removes the external identity link from the currently signed-in
    /// local account, if any.
    /// </summary>
    [HttpPost("Unlink")]
    [Authorize]
    public ActionResult Unlink()
    {
        if (User.ExternalAuthID is null)
            return NoContent();

        InvalidateProviderTokens(User.JMMUserID, User.ExternalAuthID);
        User.ExternalAuthID = null;
        userRepository.Save(User);
        return NoContent();
    }

    // Tokens are keyed by device name "OIDC — {authority} — {subject}", so a provider-scoped
    // prefix match invalidates every token minted for this user under the given authority
    // without needing to look up the OIDC settings, which may have already changed.
    private void InvalidateProviderTokens(int userID, string externalAuthID)
    {
        var authority = externalAuthID.Split("::", 2)[0];
        authTokensRepository.DeleteWithUserIDAndDevicePrefix(userID, $"OIDC — {authority}");
    }

    private async Task<ActionResult> StartAuthorizeAsync(string? returnUrl, int? linkUserID, string? forwardedProto)
    {
        var (settings, configuration, error) = await GetEnabledConfigurationAsync();
        if (error is not null)
            return error;

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
        if (!string.IsNullOrEmpty(error))
            return RedirectToWebUiWithError($"OIDC provider returned an error: {error}");

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return RedirectToWebUiWithError("Missing code or state.");

        if (UnprotectState(state) is not { } statePayload || DateTime.UtcNow - statePayload.CreatedAt > StateLifetime)
            return RedirectToWebUiWithError("Invalid or expired sign-in attempt. Please try again.");

        var (settings, configuration, configError) = await GetEnabledConfigurationAsync();
        if (configError is not null)
            return configError;

        var (idToken, exchangeError) = await ExchangeCodeForIdTokenAsync(configuration, settings, code, forwardedProto);
        if (exchangeError is not null)
            return RedirectToWebUiWithError(exchangeError);

        var (claims, validationError) = await ValidateIdTokenAsync(configuration, settings, idToken!, statePayload.Nonce);
        if (validationError is not null || claims is null)
            return RedirectToWebUiWithError(validationError ?? "ID token validation failed.");

        var rawSubject = claims.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(rawSubject))
            return RedirectToWebUiWithError("ID token is missing a subject claim.");

        // Prefix with the authority so switching Settings.Oidc.Authority can never make an
        // identity minted by a different provider resolve to the same external auth ID.
        var externalAuthID = $"{settings.Authority}::{rawSubject}";

        var (user, userError) = statePayload.LinkUserID is { } linkUserID
            ? LinkUser(linkUserID, externalAuthID)
            : await ResolveUserAsync(externalAuthID, rawSubject, settings);
        if (userError is not null)
            return RedirectToWebUiWithError(userError);

        // Match the Shoko token's lifetime to the OIDC token's own expiry rather than a
        // fixed value — a fresh login always mints a new token, so several can coexist
        // with different expirations without any one of them ever being non-expiring.
        if (GetExpiration(claims) is not { } expiresAt || expiresAt <= DateTime.UtcNow.AddMinutes(1))
            return RedirectToWebUiWithError("ID token is missing a valid expiration.");

        // Subject is part of the device name (not just externalAuthID) so a provider-scoped
        // Unlink() can target exactly the tokens minted for this identity via prefix match.
        var apiToken = await userService.GenerateApiTokenForUser(user!, $"OIDC — {settings.Authority} — {rawSubject}", expiresAt);
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
    // impersonating) the IdP take over a same-named local account. The one opt-in exception
    // is AutoCreateUsers, which provisions a brand new account rather than linking an
    // existing one, so it can't be used to take over anything.
    private async Task<(JMMUser? User, string? Error)> ResolveUserAsync(string externalAuthID, string rawSubject, OidcSettings settings)
    {
        var user = userRepository.GetByExternalAuthID(externalAuthID);
        if (user is not null)
            return (user, null);

        if (!settings.AutoCreateUsers)
            return (null, "No local Shoko account is linked to this SSO identity. Sign in locally and link your account first.");

        if (userRepository.GetByUsername(rawSubject) is not null)
            return (null, $"Cannot auto-create a user for subject \"{rawSubject}\" — that username is already taken.");

        try
        {
            var created = (JMMUser)await userService.CreateUser(new UserUpdate
            {
                Username = rawSubject,
                Password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            });
            created.ExternalAuthID = externalAuthID;
            userRepository.Save(created);
            return (created, null);
        }
        catch (GenericValidationException ex)
        {
            return (null, $"Could not auto-create a user for subject \"{rawSubject}\": {ex.Message}");
        }
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

    private async Task<(OidcSettings Settings, OpenIdConnectConfiguration Configuration, ActionResult? Error)> GetEnabledConfigurationAsync()
    {
        var settings = Settings;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Authority) || string.IsNullOrWhiteSpace(settings.ClientID))
            return (settings, null!, NotFound("OIDC sign-in is not enabled."));

        try
        {
            return (settings, await GetProviderConfigurationAsync(settings.Authority), null);
        }
        catch (Exception)
        {
            return (settings, null!, RedirectToWebUiWithError("Could not reach the OIDC provider. Please try again later."));
        }
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
