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
using Shoko.Server.API.Annotations;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Optional OpenID Connect single sign-on. Disabled unless configured in
/// Settings.Oidc. Never creates local accounts — a matching local account
/// (by username or email) must already exist; SSO only links to it and
/// mints the same kind of API token the local sign-in endpoint issues.
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

    private OidcSettings Settings => settingsProvider.GetSettings().Oidc;

    private sealed record StatePayload(string Nonce, string? ReturnUrl, DateTime CreatedAt);

    /// <summary>
    /// Redirects the browser to the configured OIDC provider's authorization
    /// endpoint.
    /// </summary>
    [HttpGet("Challenge")]
    [AllowAnonymous]
    public async Task<ActionResult> Challenge(
        [FromQuery] string? returnUrl = null,
        [FromHeader(Name = "X-Forwarded-Proto")] string? forwardedProto = null)
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
        var state = ProtectState(new StatePayload(nonce, returnUrl, DateTime.UtcNow));

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

        var subject = claims!.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
            return RedirectToWebUiWithError("ID token is missing a subject claim.");

        var (user, userError) = ResolveUser(claims, subject);
        if (userError is not null)
            return RedirectToWebUiWithError(userError);

        var apiToken = await userService.GenerateApiTokenForUser(user!, "OIDC");
        return RedirectToWebUiWithToken(apiToken.Token, user!.Username, statePayload.ReturnUrl);
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

    private (JMMUser? User, string? Error) ResolveUser(ClaimsIdentity claims, string subject)
    {
        var user = userRepository.GetByExternalAuthID(subject);
        if (user is not null)
            return (user, null);

        // First SSO login for this external identity — link to a
        // pre-existing local account by username or email, never create one.
        // Only trust the email claim for matching if the provider marked it
        // verified; an unverified email lets any IdP user claim someone else's.
        var emailVerified = bool.TryParse(claims.FindFirst("email_verified")?.Value, out var verified) && verified;
        var candidateNames = new[]
        {
            claims.FindFirst("preferred_username")?.Value,
            emailVerified ? claims.FindFirst("email")?.Value : null,
        }.Where(name => !string.IsNullOrWhiteSpace(name));

        user = candidateNames.Select(userRepository.GetByUsername).FirstOrDefault(u => u is not null);
        if (user is null)
            return (null, "No local Shoko account matches your SSO identity. Ask an admin to check your username/email.");

        user.ExternalAuthID = subject;
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

    private ActionResult RedirectToWebUiWithToken(string token, string username, string? returnUrl)
    {
        var basePath = settingsProvider.GetSettings().Web.WebUIPublicPath.TrimEnd('/');
        var target = string.IsNullOrWhiteSpace(returnUrl) ? basePath : returnUrl;
        return Redirect($"{target}#oidcToken={Uri.EscapeDataString(token)}&oidcUsername={Uri.EscapeDataString(username)}");
    }

    private ActionResult RedirectToWebUiWithError(string message)
    {
        var basePath = settingsProvider.GetSettings().Web.WebUIPublicPath.TrimEnd('/');
        return Redirect($"{basePath}#oidcError={Uri.EscapeDataString(message)}");
    }
}
