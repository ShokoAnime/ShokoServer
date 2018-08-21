using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Shoko.Server.API.Authentication
{
    public class CustomAuthHandler : AuthenticationHandler<CustomAuthOptions>
    {
        public CustomAuthHandler(IOptionsMonitor<CustomAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Get Authorization header value
            if (!Request.Headers.TryGetValue("apikey", out var authorization))
            {
                return Task.FromResult(AuthenticateResult.Fail("Cannot read authorization header."));
            }
            //Join with the query as well.
            var authkeys = authorization.Union(Request.Query["apikey"]);

            //Find authenticated user.
            var user = authorization.Select(GetUserForKey).FirstOrDefault(s => s != null);

            if (user == null) return Task.FromResult(AuthenticateResult.Fail("Invalid Authentication key"));

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(user), Options.Scheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        private static SVR_JMMUser GetUserForKey(string ctx)
        {
            if (!(ServerState.Instance?.ServerOnline ?? false)) return null;
            string apikey = ctx?.Trim();
            if (string.IsNullOrEmpty(apikey)) return null;

            AuthTokens auth = Repo.AuthTokens.GetByToken(apikey);
            return auth != null ? Repo.JMMUser.GetByID(auth.UserID) : null;
        }
    }
}
