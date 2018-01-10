namespace TvDbSharper.Clients
{
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;
    using TvDbSharper.Infrastructure;

    internal class UsersClient : IUsersClient
    {
        public UsersClient(IApiClient apiClient, IParser parser)
        {
            this.ApiClient = apiClient;
            this.Parser = parser;
            this.UrlHelpers = new UrlHelpers();
        }

        private IApiClient ApiClient { get; }

        private IParser Parser { get; }

        private UrlHelpers UrlHelpers { get; }

        public Task<TvDbResponse<UserRatings[]>> AddEpisodeRatingAsync(int episodeId, decimal rating, CancellationToken cancellationToken)
        {
            return this.AddRatingAsync(RatingType.Episode, episodeId, rating, cancellationToken);
        }

        public Task<TvDbResponse<UserRatings[]>> AddEpisodeRatingAsync(int episodeId, decimal rating)
        {
            return this.AddEpisodeRatingAsync(episodeId, rating, CancellationToken.None);
        }

        public Task<TvDbResponse<UserRatings[]>> AddImageRatingAsync(int imageId, decimal rating, CancellationToken cancellationToken)
        {
            return this.AddRatingAsync(RatingType.Image, imageId, rating, cancellationToken);
        }

        public Task<TvDbResponse<UserRatings[]>> AddImageRatingAsync(int imageId, decimal rating)
        {
            return this.AddImageRatingAsync(imageId, rating, CancellationToken.None);
        }

        public async Task<TvDbResponse<UserRatings[]>> AddRatingAsync(
            RatingType itemType,
            int itemId,
            decimal rating,
            CancellationToken cancellationToken)
        {
            var request = new ApiRequest("PUT", $"/user/ratings/{this.UrlHelpers.QuerifyEnum(itemType)}/{itemId}/{rating}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<UserRatings[]>>(response, ErrorMessages.Users.RateAsync);
        }

        public Task<TvDbResponse<UserRatings[]>> AddRatingAsync(RatingType itemType, int itemId, decimal rating)
        {
            return this.AddRatingAsync(itemType, itemId, rating, CancellationToken.None);
        }

        public Task<TvDbResponse<UserRatings[]>> AddSeriesRatingAsync(int seriesId, decimal rating, CancellationToken cancellationToken)
        {
            return this.AddRatingAsync(RatingType.Series, seriesId, rating, cancellationToken);
        }

        public Task<TvDbResponse<UserRatings[]>> AddSeriesRatingAsync(int seriesId, decimal rating)
        {
            return this.AddSeriesRatingAsync(seriesId, rating, CancellationToken.None);
        }

        public async Task<TvDbResponse<UserFavorites>> AddToFavoritesAsync(int seriesId, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("PUT", $"/user/favorites/{seriesId}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<UserFavorites>>(response, ErrorMessages.Users.AddToFavoritesAsync);
        }

        public Task<TvDbResponse<UserFavorites>> AddToFavoritesAsync(int seriesId)
        {
            return this.AddToFavoritesAsync(seriesId, CancellationToken.None);
        }

        public async Task<TvDbResponse<User>> GetAsync(CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", "/user");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<User>>(response, ErrorMessages.Users.GetAsync);
        }

        public Task<TvDbResponse<User>> GetAsync()
        {
            return this.GetAsync(CancellationToken.None);
        }

        public Task<TvDbResponse<UserRatings[]>> GetEpisodesRatingsAsync(CancellationToken cancellationToken)
        {
            return this.GetRatingsAsync(RatingType.Episode, cancellationToken);
        }

        public Task<TvDbResponse<UserRatings[]>> GetEpisodesRatingsAsync()
        {
            return this.GetEpisodesRatingsAsync(CancellationToken.None);
        }

        public async Task<TvDbResponse<UserFavorites>> GetFavoritesAsync(CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", "/user/favorites");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<UserFavorites>>(response, ErrorMessages.Users.GetFavoritesAsync);
        }

        public Task<TvDbResponse<UserFavorites>> GetFavoritesAsync()
        {
            return this.GetFavoritesAsync(CancellationToken.None);
        }

        public Task<TvDbResponse<UserRatings[]>> GetImagesRatingsAsync(CancellationToken cancellationToken)
        {
            return this.GetRatingsAsync(RatingType.Image, cancellationToken);
        }

        public Task<TvDbResponse<UserRatings[]>> GetImagesRatingsAsync()
        {
            return this.GetImagesRatingsAsync(CancellationToken.None);
        }

        public async Task<TvDbResponse<UserRatings[]>> GetRatingsAsync(CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", "/user/ratings");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<UserRatings[]>>(response, ErrorMessages.Users.GetRatingsAsync);
        }

        public async Task<TvDbResponse<UserRatings[]>> GetRatingsAsync(RatingType type, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("GET", $"/user/ratings/query?itemType={this.UrlHelpers.QuerifyEnum(type)}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<UserRatings[]>>(response, ErrorMessages.Users.GetRatingsAsync);
        }

        public Task<TvDbResponse<UserRatings[]>> GetRatingsAsync()
        {
            return this.GetRatingsAsync(CancellationToken.None);
        }

        public Task<TvDbResponse<UserRatings[]>> GetRatingsAsync(RatingType type)
        {
            return this.GetRatingsAsync(type, CancellationToken.None);
        }

        public Task<TvDbResponse<UserRatings[]>> GetSeriesRatingsAsync(CancellationToken cancellationToken)
        {
            return this.GetRatingsAsync(RatingType.Series, cancellationToken);
        }

        public Task<TvDbResponse<UserRatings[]>> GetSeriesRatingsAsync()
        {
            return this.GetSeriesRatingsAsync(CancellationToken.None);
        }

        public Task RemoveEpisodeRatingAsync(int episodeId, CancellationToken cancellationToken)
        {
            return this.RemoveRatingAsync(RatingType.Episode, episodeId, cancellationToken);
        }

        public Task RemoveEpisodeRatingAsync(int episodeId)
        {
            return this.RemoveEpisodeRatingAsync(episodeId, CancellationToken.None);
        }

        public async Task<TvDbResponse<UserFavorites>> RemoveFromFavoritesAsync(int seriesId, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("DELETE", $"/user/favorites/{seriesId}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return this.Parser.Parse<TvDbResponse<UserFavorites>>(response, ErrorMessages.Users.RemoveFromFavoritesAsync);
        }

        public Task<TvDbResponse<UserFavorites>> RemoveFromFavoritesAsync(int seriesId)
        {
            return this.RemoveFromFavoritesAsync(seriesId, CancellationToken.None);
        }

        public Task RemoveImageRatingAsync(int imageId, CancellationToken cancellationToken)
        {
            return this.RemoveRatingAsync(RatingType.Image, imageId, cancellationToken);
        }

        public Task RemoveImageRatingAsync(int imageId)
        {
            return this.RemoveImageRatingAsync(imageId, CancellationToken.None);
        }

        public async Task RemoveRatingAsync(RatingType itemType, int itemId, CancellationToken cancellationToken)
        {
            var request = new ApiRequest("DELETE", $"/user/ratings/{this.UrlHelpers.QuerifyEnum(itemType)}/{itemId}");
            var response = await this.ApiClient.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
            var data = this.Parser.Parse<TvDbResponse<UserRatings[]>>(response, ErrorMessages.Users.RemoveFromFavoritesAsync);
        }

        public Task RemoveRatingAsync(RatingType itemType, int itemId)
        {
            return this.RemoveRatingAsync(itemType, itemId, CancellationToken.None);
        }

        public Task RemoveSeriesRatingAsync(int seriesId, CancellationToken cancellationToken)
        {
            return this.RemoveRatingAsync(RatingType.Series, seriesId, cancellationToken);
        }

        public Task RemoveSeriesRatingAsync(int seriesId)
        {
            return this.RemoveSeriesRatingAsync(seriesId, CancellationToken.None);
        }
    }
}