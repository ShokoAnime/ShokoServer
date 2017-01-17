using System.Collections.Generic;
using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;
using Shoko.Models.Client;
using Shoko.Models.Metro;
using Shoko.Models.Server;

namespace Shoko.Models
{
    [RestBasePath("/api/Metro")]
    public interface IShokoServerMetro
    {
        [Rest("Server/Status",Verbs.Get)]
        CL_ServerStatus GetServerStatus();

        [Rest("Server/Settings", Verbs.Post)]
        CL_ServerSettings GetServerSettings();

        [Rest("Comment/{traktID}/{commentText}/{isSpoiler}", Verbs.Post)]
        CL_Response<bool> PostCommentShow(string traktID, string commentText, bool isSpoiler);

        [Rest("Community/Links/{animeID}", Verbs.Get)]
        Metro_CommunityLinks GetCommunityLinks(int animeID);

        [Rest("Anime/Search/{userID}/{queryText}/{maxRecords}", Verbs.Get)]
        List<Metro_Anime_Summary> SearchAnime(int userID, string queryText, int maxRecords);

        [Rest("User/Auth/{username}/{password}", Verbs.Post)]
        JMMUser AuthenticateUser(string username, string password);

        [Rest("User", Verbs.Get)]
        List<JMMUser> GetAllUsers();

        [Rest("Group/{userID}", Verbs.Get)]
        List<CL_AnimeGroup_User> GetAllGroups(int userID);

        [Rest("Anime/New/{maxRecords}/{userID}", Verbs.Get)]
        List<Metro_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int userID);

        [Rest("Anime/Detail/{animeID}/{userID}/{maxEpisodeRecords}", Verbs.Get)]
        Metro_Anime_Detail GetAnimeDetail(int animeID, int userID, int maxEpisodeRecords);

        [Rest("Anime/Summary/{animeID}", Verbs.Get)]
        Metro_Anime_Summary GetAnimeSummary(int animeID);

        [Rest("Anime/Character/{animeID}/{maxRecords}", Verbs.Get)]
        List<Metro_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords);

        [Rest("Anime/Comment/{animeID}/{maxRecords}", Verbs.Get)]
        List<Metro_Comment> GetTraktCommentsForAnime(int animeID, int maxRecords);

        [Rest("Anime/Recommendation/{animeID}/{maxRecords}", Verbs.Get)]
        List<Metro_Comment> GetAniDBRecommendationsForAnime(int animeID, int maxRecords);

        [Rest("Anime/ContinueWatch/{maxRecords}/{userID}", Verbs.Get)]
        List<Metro_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int userID);

        [Rest("Anime/Similar/{animeID}/{maxRecords}/{userID}", Verbs.Get)]
        List<Metro_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int userID);

        [Rest("Anime/Calendar/{userID}/{startDateSecs}/{endDateSecs}/{maxRecords}", Verbs.Get)]
        List<Metro_Anime_Summary> GetAnimeCalendar(int userID, int startDateSecs, int endDateSecs, int maxRecords);

        [Rest("Episode/Files/{episodeID}/{userID}", Verbs.Get)]
        List<CL_VideoDetailed> GetFilesForEpisode(int episodeID, int userID);

        [Rest("Episode/Watch/{animeEpisodeID}/{watchedStatus}/{userID}", Verbs.Get)]
        CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID, bool watchedStatus, int userID);

        [Rest("Anime/Refresh/{animeID}", Verbs.Get)]
        string UpdateAnimeData(int animeID);
    }
}