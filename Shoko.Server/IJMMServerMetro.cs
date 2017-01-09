using System.Collections.Generic;
using System.ServiceModel;
using Shoko.Models.Client;
using Shoko.Models.Server;

namespace Shoko.Models
{
    public interface IJMMServerMetro
    {
        Contract_ServerStatus GetServerStatus();

        Contract_ServerSettings GetServerSettings();

        bool PostCommentShow(string traktID, string commentText, bool isSpoiler, ref string returnMessage);
        MetroContract_CommunityLinks GetCommunityLinks(int animeID);

        List<MetroContract_Anime_Summary> SearchAnime(int jmmuserID, string queryText, int maxRecords);

        JMMUser AuthenticateUser(string username, string password);

        List<JMMUser> GetAllUsers();

        List<CL_AnimeGroup_User> GetAllGroups(int userID);

        List<MetroContract_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int jmmuserID);

        MetroContract_Anime_Detail GetAnimeDetail(int animeID, int jmmuserID, int maxEpisodeRecords);

        MetroContract_Anime_Summary GetAnimeSummary(int animeID);

        List<MetroContract_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords);

        List<MetroContract_Comment> GetTraktCommentsForAnime(int animeID, int maxRecords);

        List<MetroContract_Comment> GetAniDBRecommendationsForAnime(int animeID, int maxRecords);

        List<MetroContract_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int jmmuserID);

        List<MetroContract_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int jmmuserID);

        List<MetroContract_Anime_Summary> GetAnimeCalendar(int jmmuserID, int startDateSecs, int endDateSecs,
            int maxRecords);

        List<Contract_VideoDetailed> GetFilesForEpisode(int episodeID, int userID);

        Contract_ToggleWatchedStatusOnEpisode_Response ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus,
            int userID);

        string UpdateAnimeData(int animeID);
    }
}