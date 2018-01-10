namespace TvDbSharper
{
    using System.Threading;
    using System.Threading.Tasks;

    using TvDbSharper.Dto;

    /// <summary>
    /// Used for working with the current user
    /// </summary>
    public interface IUsersClient
    {
        /// <summary>
        /// <para>[PUT /user/ratings/{itemType}/{itemId}/{itemRating}]</para>
        /// <para>Adds a rating to an episode.</para>
        /// </summary>
        /// <param name="episodeId">The ID of the episode to be rated.</param>
        /// <param name="rating">The rating.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> AddEpisodeRatingAsync(int episodeId, decimal rating, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[PUT /user/ratings/{itemType}/{itemId}/{itemRating}]</para>
        /// <para>Adds a rating to an episode.</para>
        /// </summary>
        /// <param name="episodeId">The ID of the episode to be rated.</param>
        /// <param name="rating">The rating.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> AddEpisodeRatingAsync(int episodeId, decimal rating);

        /// <summary>
        /// <para>[PUT /user/ratings/{itemType}/{itemId}/{itemRating}]</para>
        /// <para>Adds a rating to an image.</para>
        /// </summary>
        /// <param name="imageId">The ID of the image to be rated.</param>
        /// <param name="rating">The rating.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> AddImageRatingAsync(int imageId, decimal rating, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[PUT /user/ratings/{itemType}/{itemId}/{itemRating}]</para>
        /// <para>Adds a rating to an image.</para>
        /// </summary>
        /// <param name="imageId">The ID of the image to be rated.</param>
        /// <param name="rating">The rating.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> AddImageRatingAsync(int imageId, decimal rating);

        /// <summary>
        /// <para>[PUT /user/ratings/{itemType}/{itemId}/{itemRating}]</para>
        /// <para>Adds a rating to an item.</para>
        /// </summary>
        /// <param name="itemType">An enumeration that represents the type of item to be rated. Can be  Series, Episode, Image</param>
        /// <param name="itemId">The ID of the item to be rated.</param>
        /// <param name="rating">The rating.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> AddRatingAsync(
            RatingType itemType,
            int itemId,
            decimal rating,
            CancellationToken cancellationToken);

        /// <summary>
        /// <para>[PUT /user/ratings/{itemType}/{itemId}/{itemRating}]</para>
        /// <para>Adds a rating to an item.</para>
        /// </summary>
        /// <param name="itemType">An enumeration that represents the type of item to be rated. Can be  Series, Episode, Image</param>
        /// <param name="itemId">The ID of the item to be rated.</param>
        /// <param name="rating">The rating.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> AddRatingAsync(RatingType itemType, int itemId, decimal rating);

        /// <summary>
        /// <para>[PUT /user/ratings/{itemType}/{itemId}/{itemRating}]</para>
        /// <para>Adds a rating to a series.</para>
        /// </summary>
        /// <param name="seriesId">The ID of the series to be rated.</param>
        /// <param name="rating">The rating.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> AddSeriesRatingAsync(int seriesId, decimal rating, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[PUT /user/ratings/{itemType}/{itemId}/{itemRating}]</para>
        /// <para>Adds a rating to a series.</para>
        /// </summary>
        /// <param name="seriesId">The ID of the series to be rated.</param>
        /// <param name="rating">The rating.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> AddSeriesRatingAsync(int seriesId, decimal rating);

        /// <summary>
        /// <para>[PUT /user/favorites/{id}]</para>
        /// <para>Adds the supplied series to the user’s favorite’s list and returns the updated list.</para>
        /// </summary>
        /// <param name="seriesId">The ID of the series.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserFavorites>> AddToFavoritesAsync(int seriesId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[PUT /user/favorites/{id}]</para>
        /// <para>Adds the supplied series to the user’s favorite’s list and returns the updated list.</para>
        /// </summary>
        /// <param name="seriesId">The ID of the series.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserFavorites>> AddToFavoritesAsync(int seriesId);

        /// <summary>
        /// <para>[GET /user]</para>
        /// <para>Returns basic information about the currently authenticated user.</para>
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<User>> GetAsync(CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /user]</para>
        /// <para>Returns basic information about the currently authenticated user.</para>
        /// </summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<User>> GetAsync();

        /// <summary>
        /// <para>[GET /user/ratings/query]</para>
        /// <para>Returns the episode ratings for a given user.</para>
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> GetEpisodesRatingsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /user/ratings/query]</para>
        /// <para>Returns the episode ratings for a given user.</para>
        /// </summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> GetEpisodesRatingsAsync();

        /// <summary>
        /// <para>[GET /user/favorites]</para>
        /// <para>Returns the favorite series for a given user, will be a blank array if no favorites exist.</para>
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserFavorites>> GetFavoritesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /user/favorites]</para>
        /// <para>Returns the favorite series for a given user, will be a blank array if no favorites exist.</para>
        /// </summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserFavorites>> GetFavoritesAsync();

        /// <summary>
        /// <para>[GET /user/ratings/query]</para>
        /// <para>Returns the image ratings for a given user.</para>
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> GetImagesRatingsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /user/ratings/query]</para>
        /// <para>Returns the image ratings for a given user.</para>
        /// </summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> GetImagesRatingsAsync();

        /// <summary>
        /// <para>[GET /user/ratings]</para>
        /// <para>Returns the ratings for the given user.</para>
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> GetRatingsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /user/ratings]</para>
        /// <para>Returns the ratings for the given user.</para>
        /// </summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> GetRatingsAsync();

        /// <summary>
        /// <para>[GET /user/ratings/query]</para>
        /// <para>Returns ratings for a given user that match the rating type.</para>
        /// </summary>
        /// <param name="type">An enumeration that represents the type of rating to be retrieved. Can be  Series, Episode, Image</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> GetRatingsAsync(RatingType type, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /user/ratings/query]</para>
        /// <para>Returns ratings for a given user that match the rating type.</para>
        /// </summary>
        /// <param name="type">An enumeration that represents the type of rating to be retrieved. Can be  Series, Episode, Image</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> GetRatingsAsync(RatingType type);

        /// <summary>
        /// <para>[GET /user/ratings/query]</para>
        /// <para>Returns the series ratings for a given user.</para>
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> GetSeriesRatingsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// <para>[GET /user/ratings/query]</para>
        /// <para>Returns the series ratings for a given user.</para>
        /// </summary>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserRatings[]>> GetSeriesRatingsAsync();

        /// <summary>
        /// <para>[DELETE /user/ratings/{itemType}/{itemId}]</para>
        /// <para>Removes the user rating from an episode.</para>
        /// </summary>
        /// <param name="episodeId">The ID of the episode.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task RemoveEpisodeRatingAsync(int episodeId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[DELETE /user/ratings/{itemType}/{itemId}]</para>
        /// <para>Removes the user rating from an episode.</para>
        /// </summary>
        /// <param name="episodeId">The ID of the episode.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task RemoveEpisodeRatingAsync(int episodeId);

        /// <summary>
        /// <para>[DELETE /user/favorites/{id}]</para>
        /// <para>Deletes the given series from the user’s favorite’s list and returns the updated list.</para>
        /// </summary>
        /// <param name="seriesId">The ID of the series.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserFavorites>> RemoveFromFavoritesAsync(int seriesId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[DELETE /user/favorites/{id}]</para>
        /// <para>Deletes the given series from the user’s favorite’s list and returns the updated list.</para>
        /// </summary>
        /// <param name="seriesId">The ID of the series.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task`1" />.The task object representing the asynchronous operation.</returns>
        Task<TvDbResponse<UserFavorites>> RemoveFromFavoritesAsync(int seriesId);

        /// <summary>
        /// <para>[DELETE /user/ratings/{itemType}/{itemId}]</para>
        /// <para>Removes the user rating from an image.</para>
        /// </summary>
        /// <param name="imageId">The ID of the image.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task RemoveImageRatingAsync(int imageId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[DELETE /user/ratings/{itemType}/{itemId}]</para>
        /// <para>Removes the user rating from an image.</para>
        /// </summary>
        /// <param name="imageId">The ID of the image.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task RemoveImageRatingAsync(int imageId);

        /// <summary>
        /// <para>[DELETE /user/ratings/{itemType}/{itemId}]</para>
        /// <para>Removes the user rating from an item.</para>
        /// </summary>
        /// <param name="itemType">An enumeration that represents the type of item to be rated. Can be  Series, Episode, Image</param>
        /// <param name="itemId">The ID of the item.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task RemoveRatingAsync(RatingType itemType, int itemId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[DELETE /user/ratings/{itemType}/{itemId}]</para>
        /// <para>Removes the user rating from an item.</para>
        /// </summary>
        /// <param name="itemType">An enumeration that represents the type of item to be rated. Can be  Series, Episode, Image</param>
        /// <param name="itemId">The ID of the item.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task RemoveRatingAsync(RatingType itemType, int itemId);

        /// <summary>
        /// <para>[DELETE /user/ratings/{itemType}/{itemId}]</para>
        /// <para>Removes the user rating from a series.</para>
        /// </summary>
        /// <param name="seriesId">The ID of the series.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task RemoveSeriesRatingAsync(int seriesId, CancellationToken cancellationToken);

        /// <summary>
        /// <para>[DELETE /user/ratings/{itemType}/{itemId}]</para>
        /// <para>Removes the user rating from a series.</para>
        /// </summary>
        /// <param name="seriesId">The ID of the series.</param>
        /// <returns>Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.</returns>
        Task RemoveSeriesRatingAsync(int seriesId);
    }
}