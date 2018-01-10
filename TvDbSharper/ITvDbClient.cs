namespace TvDbSharper
{
    public interface ITvDbClient
    {
        string AcceptedLanguage { get; set; }

        /// <summary>
        /// Used for obtaining and refreshing your JWT token
        /// </summary>
        IAuthenticationClient Authentication { get; }

        string BaseUrl { get; set; }

        /// <summary>
        /// Used for getting information about a specific episode
        /// </summary>
        IEpisodesClient Episodes { get; }

        /// <summary>
        /// Used for geting available languages and information about them
        /// </summary>
        ILanguagesClient Languages { get; }

        /// <summary>
        /// Used for searching for a particular series
        /// </summary>
        ISearchClient Search { get; }

        /// <summary>
        /// Used for getting information about a specific series
        /// </summary>
        ISeriesClient Series { get; }

        /// <summary>
        /// Used for getting series that have been recently updated
        /// </summary>
        IUpdatesClient Updates { get; }

        /// <summary>
        /// Used for working with the current user
        /// </summary>
        IUsersClient Users { get; }
    }
}