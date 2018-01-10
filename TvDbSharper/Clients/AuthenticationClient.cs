namespace TvDbSharper.Clients
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    using TvDbSharper.Dto;
    using TvDbSharper.Infrastructure;

    internal class AuthenticationClient : IAuthenticationClient
    {
        private const string AuthorizationHeaderName = "Authorization";

        private const string ContentTypeHeaderName = "Content-Type";

        private readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public AuthenticationClient(IApiClient apiClient, IParser parser)
        {
            this.ApiClient = apiClient;
            this.Parser = parser;
        }

        public string Token
        {
            get
            {
                var headers = this.ApiClient.DefaultRequestHeaders;

                if (headers.ContainsKey(AuthorizationHeaderName))
                {
                    return headers[AuthorizationHeaderName].Split(' ')[1];
                }

                return null;
            }

            set => this.ApiClient.DefaultRequestHeaders[AuthorizationHeaderName] = "Bearer " + value;
        }

        private IApiClient ApiClient { get; }

        private IParser Parser { get; }

        public async Task AuthenticateAsync(AuthenticationData authenticationData, CancellationToken cancellationToken)
        {
            if (authenticationData == null)
            {
                throw new ArgumentNullException(nameof(authenticationData));
            }

            string body = JsonConvert.SerializeObject(authenticationData, this.serializerSettings);
     
            var request = new ApiRequest("POST", "/login", body)
            {
                Headers = new Dictionary<string, string>
                {
                    [ContentTypeHeaderName] = "application/json"
                }
            };

            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            var data = this.Parser.Parse<AuthenticationResponse>(response, ErrorMessages.Authentication.AuthenticateAsync);

            this.UpdateAuthenticationHeader(data.Token);
        }

        public Task AuthenticateAsync(string apiKey, string username, string userKey, CancellationToken cancellationToken)
        {
            return this.AuthenticateAsync(new AuthenticationData(apiKey, username, userKey), cancellationToken);
        }

        public Task AuthenticateAsync(string apiKey, string username, string userKey)
        {
            return this.AuthenticateAsync(apiKey, username, userKey, CancellationToken.None);
        }

        public Task AuthenticateAsync(AuthenticationData authenticationData)
        {
            return this.AuthenticateAsync(authenticationData, CancellationToken.None);
        }

        public Task AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
        {
            return this.AuthenticateAsync(new AuthenticationData(apiKey), cancellationToken);
        }

        public Task AuthenticateAsync(string apiKey)
        {
            return this.AuthenticateAsync(apiKey, CancellationToken.None);
        }

        public async Task RefreshTokenAsync(CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", "/refresh_token");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            var data = this.Parser.Parse<AuthenticationResponse>(response, ErrorMessages.Authentication.RefreshTokenAsync);

            this.UpdateAuthenticationHeader(data.Token);
        }

        public Task RefreshTokenAsync()
        {
            return this.RefreshTokenAsync(CancellationToken.None);
        }

        private void UpdateAuthenticationHeader(string token)
        {
            this.ApiClient.DefaultRequestHeaders[AuthorizationHeaderName] = "Bearer " + token;
        }
    }
}