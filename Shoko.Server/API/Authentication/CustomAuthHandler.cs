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
using System.Security.Principal;
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
            if (!ServerState.Instance.ServerOnline)
            {
                return Task.FromResult(
                    AuthenticateResult.Success(
                        new AuthenticationTicket(new ClaimsPrincipal(InitUser.Instance), Options.Scheme)));
            }
            
            
            // Get Authorization header value and join with the query
            var authkeys = Request.Headers["apikey"].Union(Request.Query["apikey"]).ToList();

            if (authkeys.Count == 0)
            {
                return Task.FromResult(AuthenticateResult.Fail("Cannot read authorization header or query."));
            }

            //Find authenticated user.
            var user = authkeys.Select(GetUserForKey).FirstOrDefault(s => s != null);

            if (user == null) return Task.FromResult(AuthenticateResult.Fail("Invalid Authentication key"));

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(user), Options.Scheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        private static SVR_JMMUser GetUserForKey(string ctx)
        {
            if (!(ServerState.Instance?.ServerOnline ?? false)) return null;
            string apikey = ctx?.Trim();
            if (string.IsNullOrEmpty(apikey)) return null;

            AuthTokens auth = RepoFactory.AuthTokens.GetByToken(apikey);
            return auth != null ? RepoFactory.JMMUser.GetByID(auth.UserID) : null;
        }
    }
}
