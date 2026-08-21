using System;
using System.Collections.Concurrent;
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
using Microsoft.Extensions.Logging;
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
    ILogger<OidcAuthController> logger,
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

    // One long-lived ConfigurationManager per authority — avoids re-fetching the discovery document on every login.
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> ConfigManagers = new();

    private OidcSettings Settings => settingsProvider.GetSettings().Oidc;

    // Signed/encrypted via IDataProtector — LinkUserID and CodeVerifier are never attacker-controlled.
    private sealed record StatePayload(string Nonce, string CodeVerifier, string? ReturnUrl, DateTime CreatedAt, int? LinkUserID = null);

    /// <summary>
    /// Redirects the browser to the configured OIDC provider's authorization
    /// endpoint.
    /// </summary>
    [HttpGet("Challenge")]
    [AllowAnonymous]
    public async Task<ActionResult> Challenge([FromQuery] string? returnUrl = null)
        => await StartAuthorizeAsync(returnUrl, linkUserID: null);

    /// <summary>
    /// Starts the OIDC flow to link the currently signed-in local account to
    /// an external identity. Unlike <see cref="Challenge"/>, this requires an
    /// authenticated session and never signs in as a different user — the
    /// callback only ever links to the account that started this flow.
    /// </summary>
    [HttpGet("Link")]
    [Authorize]
    public async Task<ActionResult> Link([FromQuery] string? returnUrl = null)
        => await StartAuthorizeAsync(returnUrl, linkUserID: User.JMMUserID);

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

    // Tokens are keyed by device name "OIDC — {authority} — {subject}", so a prefix match invalidates them all.
    private void InvalidateProviderTokens(int userID, string externalAuthID)
    {
        var authority = externalAuthID.Split("::", 2)[0];
        authTokensRepository.DeleteWithUserIDAndDevicePrefix(userID, $"OIDC — {authority}");
    }

    private async Task<ActionResult> StartAuthorizeAsync(string? returnUrl, int? linkUserID)
    {
        var (settings, configuration, error) = await GetEnabledConfigurationAsync();
        if (error is not null)
            return error;

        // Local-only — the token is delivered via URL fragment on redirect, so an open redirect here would leak it.
        var safeReturnUrl = returnUrl is not null && Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        var nonce = Guid.NewGuid().ToString("N");
        var codeVerifier = GeneratePkceCodeVerifier();
        var state = ProtectState(new StatePayload(nonce, codeVerifier, safeReturnUrl, DateTime.UtcNow, linkUserID));

        var authorizeUrl = QueryHelpers.AddQueryString(configuration.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = settings.ClientID,
            ["response_type"] = "code",
            ["scope"] = "openid profile email",
            ["redirect_uri"] = BuildRedirectUri(settings),
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = ComputePkceCodeChallenge(codeVerifier),
            ["code_challenge_method"] = "S256",
        });

        return Redirect(authorizeUrl);
    }

    // OAuth 2.1 mandates PKCE for every client — closes the code-interception gap even with a client secret.
    private static string GeneratePkceCodeVerifier()
        => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private static string ComputePkceCodeChallenge(string codeVerifier)
        => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

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
        [FromQuery] string? error)
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

        var (idToken, exchangeError) = await ExchangeCodeForIdTokenAsync(configuration, settings, code, statePayload.CodeVerifier);
        if (exchangeError is not null)
            return RedirectToWebUiWithError(exchangeError);

        var (claims, validationError) = await ValidateIdTokenAsync(configuration, settings, idToken, statePayload.Nonce);
        if (claims is null)
        {
            // Cached signing keys may predate an IdP key rotation — refresh and retry once before giving up.
            RequestConfigurationRefresh(settings.Authority);
            configuration = await GetProviderConfigurationAsync(settings.Authority);
            (claims, validationError) = await ValidateIdTokenAsync(configuration, settings, idToken, statePayload.Nonce);
            if (claims is null)
                return RedirectToWebUiWithError(validationError ?? "ID token validation failed.");
        }

        var rawSubject = claims.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(rawSubject))
            return RedirectToWebUiWithError("ID token is missing a subject claim.");

        // Prefixed with authority so identities from different providers never collide.
        var externalAuthID = $"{settings.Authority}::{rawSubject}";

        var (user, userError) = statePayload.LinkUserID is { } linkUserID
            ? LinkUser(linkUserID, externalAuthID)
            : await ResolveUserAsync(externalAuthID, rawSubject, settings);
        if (userError is not null)
            return RedirectToWebUiWithError(userError);

        // Token lifetime matches the OIDC token's own expiry rather than a fixed value.
        if (GetExpiration(claims) is not { } expiresAt || expiresAt <= DateTime.UtcNow.AddMinutes(1))
            return RedirectToWebUiWithError("ID token is missing a valid expiration.");

        var apiToken = await userService.GenerateApiTokenForUser(user, $"OIDC — {settings.Authority} — {rawSubject}", expiresAt);
        return RedirectToWebUiWithToken(apiToken.Token, statePayload.ReturnUrl);
    }

    private async Task<(string? IdToken, string? Error)> ExchangeCodeForIdTokenAsync(OpenIdConnectConfiguration configuration, OidcSettings settings, string code, string codeVerifier)
    {
        var client = httpClientFactory.CreateClient("Default");
        using var tokenResponse = await client.PostAsync(configuration.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = BuildRedirectUri(settings),
            ["client_id"] = settings.ClientID,
            ["client_secret"] = settings.ClientSecret ?? string.Empty,
            ["code_verifier"] = codeVerifier,
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
        // Non-null guaranteed by GetEnabledConfigurationAsync's check, but that doesn't cross the method boundary.
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Authority);

        var handler = new JsonWebTokenHandler();
        var validationResult = await handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            // Pinned to configured Authority, not the self-declared discovery-document issuer — see GetEnabledConfigurationAsync.
            ValidIssuer = settings.Authority.TrimEnd('/'),
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

    // Never matches by username/email (account-takeover risk) — only an explicit prior Link(), or AutoCreateUsers provisioning a new account.
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
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Authority) || string.IsNullOrWhiteSpace(settings.ClientID) || string.IsNullOrWhiteSpace(settings.PublicUrl))
            return (settings, null, NotFound("OIDC sign-in is not enabled."));

        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await GetProviderConfigurationAsync(settings.Authority);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch OIDC discovery document from authority {Authority}", settings.Authority);
            return (settings, null, RedirectToWebUiWithError("Could not reach the OIDC provider. Please try again later."));
        }

        // Self-declared discovery-document issuer is only trusted once it matches the admin-configured authority.
        if (!string.Equals(configuration.Issuer?.TrimEnd('/'), settings.Authority.TrimEnd('/'), StringComparison.Ordinal))
        {
            logger.LogWarning("OIDC discovery document issuer {Issuer} does not match configured authority {Authority}", configuration.Issuer, settings.Authority);
            return (settings, null, RedirectToWebUiWithError("OIDC provider configuration mismatch. Please contact your administrator."));
        }

        return (settings, configuration, null);
    }

    private static Task<OpenIdConnectConfiguration> GetProviderConfigurationAsync(string authority)
    {
        // One long-lived instance per authority — a new manager per request would re-fetch the discovery document every login.
        var manager = ConfigManagers.GetOrAdd(authority, static a => new ConfigurationManager<OpenIdConnectConfiguration>(
            a.TrimEnd('/') + "/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever()));
        return manager.GetConfigurationAsync();
    }

    private static void RequestConfigurationRefresh(string authority)
    {
        if (ConfigManagers.TryGetValue(authority, out var manager))
            manager.RequestRefresh();
    }

    // Fixed, admin-configured redirect_uri instead of Host-header-derived — avoids a Host-header-trust surface for no benefit.
    private static string BuildRedirectUri(OidcSettings settings)
    {
        // Non-null guaranteed by GetEnabledConfigurationAsync's check, but that doesn't cross the method boundary.
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.PublicUrl);
        return $"{settings.PublicUrl.TrimEnd('/')}/api/v3/Auth/Oidc/Callback";
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
