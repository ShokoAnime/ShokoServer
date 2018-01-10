namespace TvDbSharper.Infrastructure
{
    using System.Collections.Generic;

    internal static class ErrorMessages
    {
        static ErrorMessages()
        {
            Authentication = new AuthenticationMessages();
            Episodes = new EpisodesMessages();
            Languages = new LanguagesMessages();
            Search = new SearchMessages();
            Updates = new UpdatesMessages();
            Series = new SeriesMessages();
            Users = new UsersMessages();
        }

        public static AuthenticationMessages Authentication { get; }

        public static EpisodesMessages Episodes { get; }

        public static LanguagesMessages Languages { get; }

        public static SearchMessages Search { get; }

        public static SeriesMessages Series { get; }

        public static UpdatesMessages Updates { get; }

        public static UsersMessages Users { get; }
    }

    internal class AuthenticationMessages
    {
        public IReadOnlyDictionary<int, string> AuthenticateAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Invalid credentials" }
        };

        public IReadOnlyDictionary<int, string> RefreshTokenAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" }
        };
    }

    internal class EpisodesMessages
    {
        public IReadOnlyDictionary<int, string> GetAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "The given episode ID does not exist" }
        };
    }

    internal class LanguagesMessages
    {
        public IReadOnlyDictionary<int, string> GetAllAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" }
        };

        public IReadOnlyDictionary<int, string> GetAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "The given language ID does not exist" }
        };
    }

    internal class SeriesMessages
    {
        public IReadOnlyDictionary<int, string> GetAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "The given series ID does not exist" }
        };

        // ReSharper disable once InconsistentNaming
        public IReadOnlyDictionary<int, string> GetImagesAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "The given series ID does not exist or the query returns no results" }
        };
    }

    internal class SearchMessages
    {
        public IReadOnlyDictionary<int, string> SearchSeriesAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "No records are found that match your query" }
        };
    }

    internal class UpdatesMessages
    {
        public IReadOnlyDictionary<int, string> GetAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "No records exist for the given timespan" }
        };
    }

    internal class UsersMessages
    {
        public IReadOnlyDictionary<int, string> AddToFavoritesAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "No information exists for the current user" },
            { 409, "Requested record could not be updated" }
        };

        public IReadOnlyDictionary<int, string> GetAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "No information exists for the current user" }
        };

        public IReadOnlyDictionary<int, string> GetFavoritesAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "No information exists for the current user" }
        };

        public IReadOnlyDictionary<int, string> GetRatingsAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "No information exists for the current user" }
        };

        public IReadOnlyDictionary<int, string> RateAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "No rating is found that matches your given parameters" }
        };

        public IReadOnlyDictionary<int, string> RemoveFromFavoritesAsync { get; } = new Dictionary<int, string>
        {
            { 401, "Your JWT token is missing or expired" },
            { 404, "No information exists for the current user" },
            { 409, "Requested record could not be deleted" }
        };
    }
}