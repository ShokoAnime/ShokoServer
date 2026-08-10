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

    // Discovery documents are cached/auto-refreshed per authority by ConfigurationManager
    // itself — creating a new one per request would re-fetch on every single login. Keyed
    // by authority so a change to Settings.Oidc.Authority picks up a fresh manager instead
    // of reusing a stale one.
    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> ConfigManagers = new();

    private OidcSettings Settings => settingsProvider.GetSettings().Oidc;

    // LinkUserID is only ever populated by the authenticated Link endpoint, never
    // attacker-controlled — the payload is signed/encrypted by IDataProtector.
    // CodeVerifier is the PKCE verifier generated for this attempt; only ever compared
    // against what we send back to the token endpoint under our own control.
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

    // Tokens are keyed by device name "OIDC — {authority} — {subject}", so a provider-scoped
    // prefix match invalidates every token minted for this user under the given authority
    // without needing to look up the OIDC settings, which may have already changed.
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

        // returnUrl must be a local path — otherwise a crafted Challenge/Link link could redirect
        // the freshly minted API token (delivered via URL fragment) to an attacker-controlled origin.
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

    // OAuth 2.1 mandates PKCE for every client, confidential or not — it closes the
    // authorization-code-interception gap even when a client secret is also in play.
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
            // The cached ConfigurationManager (see GetProviderConfigurationAsync) can be
            // serving a signing key set from before the IdP rotated its keys — force a refresh
            // and retry once before giving up, same as OpenIdConnectHandler does internally.
            RequestConfigurationRefresh(settings.Authority);
            configuration = await GetProviderConfigurationAsync(settings.Authority);
            (claims, validationError) = await ValidateIdTokenAsync(configuration, settings, idToken, statePayload.Nonce);
            if (claims is null)
                return RedirectToWebUiWithError(validationError ?? "ID token validation failed.");
        }

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
        // Guaranteed non-null by GetEnabledConfigurationAsync's early-return check, but that
        // guarantee doesn't cross the method boundary for the compiler's nullable analysis.
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Authority);

        var handler = new JsonWebTokenHandler();
        var validationResult = await handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            // Pinned to the configured Authority rather than configuration.Issuer — trusting
            // whatever issuer the discovery document self-declares is circular; the document
            // can only be believed once it's already been confirmed to describe our authority
            // (checked in GetEnabledConfigurationAsync).
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

        // The discovery document is allowed to self-declare its issuer, but we only trust
        // that declaration once it's confirmed to match the authority the admin configured
        // — otherwise a compromised or misdirected discovery fetch could redefine its own
        // trust anchor and ValidateIdTokenAsync's ValidIssuer check would be meaningless.
        if (!string.Equals(configuration.Issuer?.TrimEnd('/'), settings.Authority.TrimEnd('/'), StringComparison.Ordinal))
        {
            logger.LogWarning("OIDC discovery document issuer {Issuer} does not match configured authority {Authority}", configuration.Issuer, settings.Authority);
            return (settings, null, RedirectToWebUiWithError("OIDC provider configuration mismatch. Please contact your administrator."));
        }

        return (settings, configuration, null);
    }

    private static Task<OpenIdConnectConfiguration> GetProviderConfigurationAsync(string authority)
    {
        // ConfigurationManager caches and auto-refreshes internally — instantiating a new one
        // per request would defeat that and re-fetch the discovery document (and JWKS) on
        // every single login attempt. One long-lived instance per configured authority.
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

    // A fixed, admin-configured redirect_uri rather than one derived from the request's Host
    // header — OIDC providers require the exact redirect_uri to be pre-registered anyway, so
    // deriving it dynamically only adds a Host-header-trust surface for no benefit.
    private static string BuildRedirectUri(OidcSettings settings)
    {
        // Guaranteed non-null by GetEnabledConfigurationAsync's early-return check, but that
        // guarantee doesn't cross the method boundary for the compiler's nullable analysis.
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
