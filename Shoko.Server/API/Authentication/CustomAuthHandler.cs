using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shoko.Server.Models.Internal;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Server;

namespace Shoko.Server.API.Authentication;

public class CustomAuthHandler : AuthenticationHandler<CustomAuthOptions>
{
    private readonly AuthTokensRepository _authTokens;
    private readonly JMMUserRepository _users;

    public CustomAuthHandler(IOptionsMonitor<CustomAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder, AuthTokensRepository authTokens, JMMUserRepository users)
        : base(options, logger, encoder)
    {
        _authTokens = authTokens;
        _users = users;
    }

    private const string BearerPrefix = "Bearer ";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!ServerState.Instance.ServerOnline)
        {
            var initPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.Role, "init"),
                    new Claim(ClaimTypes.NameIdentifier, InitUser.Instance.JMMUserID.ToString()),
                    new Claim(ClaimTypes.AuthenticationMethod, "init")
                }, CustomAuthOptions.DefaultScheme));
            initPrincipal.AddIdentity(new ClaimsIdentity(InitUser.Instance));

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(initPrincipal, Options.Scheme)));
        }

        // Get Authorization header value and join with the query
        var authKeys = Request.Headers["apikey"]
            .Union(Request.Query["apikey"])
            .ToList();

        // SignalR auth handling.
        if (authKeys.Count == 0 && Request.Path.ToString().StartsWith("/signalr/"))
            authKeys = Request.Headers.Authorization.Where(a => a.StartsWith(BearerPrefix)).Select(a => a[BearerPrefix.Length..])
                .Union(Request.Query["access_token"])
                .ToList();

        if (authKeys.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("Cannot read authorization header or query."));
        }

        //Find authenticated user.
        var (user, token) = authKeys.Select(GetUserForKey).FirstOrDefault(s => s.user != null);

        if (user == null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authentication key"));
        }


        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "user"),
            new(ClaimTypes.NameIdentifier, user.JMMUserID.ToString()),
            new(ClaimTypes.AuthenticationMethod, "apikey"),
            new("apikey", token.Token),
            new("apikey.device", token.DeviceName),
        };
        if (user.IsAdmin == 1)
        {
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Options.Scheme));
        principal.AddIdentity(new ClaimsIdentity(user));

        var ticket = new AuthenticationTicket(principal, Options.Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private (JMMUser user, AuthTokens apikey) GetUserForKey(string ctx)
    {
        if (!(ServerState.Instance?.ServerOnline ?? false))
        {
            return (null, null);
        }

        var apikey = ctx?.Trim();
        if (string.IsNullOrEmpty(apikey))
        {
            return (null, null);
        }

        var auth = _authTokens.GetByToken(apikey);
        return (
            auth != null ? _users.GetByID(auth.UserID) : null,
            auth
        );
    }
}
