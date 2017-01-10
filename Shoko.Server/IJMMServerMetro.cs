using System.Collections.Generic;
using System.ServiceModel;
using Shoko.Models.Client;
using Shoko.Models.Metro;
using Shoko.Models.Server;

namespace Shoko.Models
{
    public interface IJMMServerMetro
    {
        CL_ServerStatus GetServerStatus();

        CL_ServerSettings GetServerSettings();

        bool PostCommentShow(string traktID, string commentText, bool isSpoiler, ref string returnMessage);
        Metro_CommunityLinks GetCommunityLinks(int animeID);

        List<Metro_Anime_Summary> SearchAnime(int jmmuserID, string queryText, int maxRecords);

        JMMUser AuthenticateUser(string username, string password);

        List<JMMUser> GetAllUsers();

        List<CL_AnimeGroup_User> GetAllGroups(int userID);

        List<Metro_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int jmmuserID);

        Metro_Anime_Detail GetAnimeDetail(int animeID, int jmmuserID, int maxEpisodeRecords);

        Metro_Anime_Summary GetAnimeSummary(int animeID);

        List<Metro_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords);

        List<Metro_Comment> GetTraktCommentsForAnime(int animeID, int maxRecords);

        List<Metro_Comment> GetAniDBRecommendationsForAnime(int animeID, int maxRecords);

        List<Metro_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int jmmuserID);

        List<Metro_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int jmmuserID);

        List<Metro_Anime_Summary> GetAnimeCalendar(int jmmuserID, int startDateSecs, int endDateSecs,
            int maxRecords);

        List<CL_VideoDetailed> GetFilesForEpisode(int episodeID, int userID);

        CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus,
            int userID);

        string UpdateAnimeData(int animeID);
    }
}