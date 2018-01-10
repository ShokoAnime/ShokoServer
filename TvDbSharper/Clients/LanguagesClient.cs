namespace TvDbSharper.Clients
{
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;
    using TvDbSharper.Infrastructure;

    internal class LanguagesClient : ILanguagesClient
    {
        public LanguagesClient(IApiClient apiClient, IParser parser)
        {
            this.ApiClient = apiClient;
            this.Parser = parser;
        }

        private IApiClient ApiClient { get; }

        private IParser Parser { get; }

        public async Task<TvDbResponse<Language[]>> GetAllAsync(CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", "/languages");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<Language[]>>(response, ErrorMessages.Languages.GetAllAsync);
        }

        public Task<TvDbResponse<Language[]>> GetAllAsync()
        {
            return this.GetAllAsync(CancellationToken.None);
        }

        public async Task<TvDbResponse<Language>> GetAsync(int languageId, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/languages/{languageId}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<Language>>(response, ErrorMessages.Languages.GetAsync);
        }

        public Task<TvDbResponse<Language>> GetAsync(int languageId)
        {
            return this.GetAsync(languageId, CancellationToken.None);
        }
    }
}