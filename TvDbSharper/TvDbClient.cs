namespace TvDbSharper
{
    using System;

    using TvDbSharper.Clients;
    using TvDbSharper.Infrastructure;

    public class TvDbClient : ITvDbClient
    {
        private const string AcceptLanguageHeaderName = "Accept-Language";

        private const string DefaultAcceptedLanguage = "en";

        private const string DefaultBaseUrl = "https://api.thetvdb.com";

        public TvDbClient()
            : this(new ApiClient(), new Parser())
        {
        }

        internal TvDbClient(IApiClient apiClient, IParser parser)
        {
            this.ApiClient = apiClient;

            this.BaseUrl = DefaultBaseUrl;

            this.Authentication = new AuthenticationClient(this.ApiClient, parser);
            this.Episodes = new EpisodesClient(this.ApiClient, parser);
            this.Languages = new LanguagesClient(this.ApiClient, parser);
            this.Search = new SearchClient(this.ApiClient, parser);
            this.Series = new SeriesClient(this.ApiClient, parser);
            this.Updates = new UpdatesClient(this.ApiClient, parser);
            this.Users = new UsersClient(this.ApiClient, parser);
        }

        public string AcceptedLanguage
        {
            get
            {
                var headers = this.ApiClient.DefaultRequestHeaders;

                if (headers.ContainsKey(AcceptLanguageHeaderName))
                {
                    return headers[AcceptLanguageHeaderName];
                }

                return DefaultAcceptedLanguage;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("The value cannot be an empty string or white space.");
                }

                this.ApiClient.DefaultRequestHeaders[AcceptLanguageHeaderName] = value;
            }
        }

        /// <summary>
        /// Used for obtaining and refreshing your JWT token
        /// </summary>
        public IAuthenticationClient Authentication { get; }

        public string BaseUrl
        {
            get => this.ApiClient.BaseAddress;

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("The value cannot be an empty string or white space.");
                }

                this.ApiClient.BaseAddress = value;
            }
        }

        /// <summary>
        /// Used for getting information about a specific episode
        /// </summary>
        public IEpisodesClient Episodes { get; }

        /// <summary>
        /// Used for getting available languages and information about them
        /// </summary>
        public ILanguagesClient Languages { get; }

        /// <summary>
        /// Used for searching for a particular series
        /// </summary>
        public ISearchClient Search { get; }

        /// <summary>
        /// Used for getting information about a specific series
        /// </summary>
        public ISeriesClient Series { get; }

        /// <summary>
        /// Used for getting series that have been recently updated
        /// </summary>
        public IUpdatesClient Updates { get; }

        /// <summary>
        /// Used for working with the current user
        /// </summary>
        public IUsersClient Users { get; }

        private IApiClient ApiClient { get; }
    }
}