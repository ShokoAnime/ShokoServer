using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shoko.Abstractions.Core.Services;
using Shoko.Server.Models.Internal;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.API.Authentication;

public class CustomAuthHandler : AuthenticationHandler<CustomAuthOptions>
{
    private readonly ISystemService _systemService;

    private readonly AuthTokensRepository _authTokens;

    private readonly ShokoUserRepository _users;

    public CustomAuthHandler(IOptionsMonitor<CustomAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemService systemService, AuthTokensRepository authTokens, ShokoUserRepository users)
        : base(options, logger, encoder)
    {
        _systemService = systemService;
        _authTokens = authTokens;
        _users = users;
    }

    private const string BearerPrefix = "Bearer ";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Collect auth tokens from the request headers and query parameters.
        var authKeys = Request.Headers["apikey"]
            .Union(Request.Headers.Authorization.OfType<string>().Where(a => a.StartsWith(BearerPrefix)).Select(a => a[BearerPrefix.Length..]))
            .Union(Request.Query["apikey"])
            .ToList();

        // SignalR auth handling.
        if (authKeys.Count is 0 && Request.HttpContext.GetEndpoint()?.Metadata.GetMetadata<HubMetadata>() is not null)
            authKeys = Request.Query["access_token"].ToList();

        if (!_systemService.IsStarted)
        {
            // In setup mode or if setup fails, give access to any APIs with the setup role, with the setup token as
            // the key while one is set.
            if (_systemService.InSetupMode || _systemService.StartupFailedException is not null)
            {
                if ((_systemService as SystemService)?.SetupToken is { IsRequired: true } setupToken && !authKeys.Any(setupToken.Matches))
                    return Task.FromResult(authKeys.Count is 0 ? AuthenticateResult.NoResult() : AuthenticateResult.Fail("Invalid Authentication key"));

                var initPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new(ClaimTypes.Role, "init"),
                        new(ClaimTypes.NameIdentifier, InitUser.Instance.ID.ToString()),
                        new(ClaimTypes.AuthenticationMethod, "init")
                    ], CustomAuthOptions.DefaultScheme));
                initPrincipal.AddIdentity(new(InitUser.Instance));
                return Task.FromResult(AuthenticateResult.Success(new(initPrincipal, Options.Scheme)));
            }

            return Task.FromResult(AuthenticateResult.Fail("Server not fully started yet"));
        }

        if (authKeys.Count is 0)
            return Task.FromResult(AuthenticateResult.NoResult());

        //Find authenticated user.
        var (user, token) = authKeys.Select(GetUserForKey).FirstOrDefault(s => s.user != null);
        if (user is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authentication key"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "user"),
            new(ClaimTypes.NameIdentifier, user.ID.ToString()),
            new(ClaimTypes.AuthenticationMethod, "apikey"),
            new("apikey", token!.Token),
        };
        if (user.IsAdmin is 1)
            claims.Add(new Claim(ClaimTypes.Role, "admin"));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Options.Scheme));
        principal.AddIdentity(new ClaimsIdentity(user));

        var ticket = new AuthenticationTicket(principal, Options.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private (ShokoUser? user, AuthTokens? apikey) GetUserForKey(string? token)
    {
        if (!_systemService.IsStarted)
            return (null, null);

        var auth = _authTokens.GetByToken(token);
        return (
            auth != null ? _users.GetByID(auth.UserID) : null,
            auth
        );
    }
}
