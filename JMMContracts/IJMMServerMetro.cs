using System.Collections.Generic;
using System.ServiceModel;

namespace JMMContracts
{
    [ServiceContract]
    public interface IJMMServerMetro
    {
        [OperationContract]
        Contract_ServerStatus GetServerStatus();

        [OperationContract]
        Contract_ServerSettings GetServerSettings();

        [OperationContract]
        bool PostCommentShow(string traktID, string commentText, bool isSpoiler, ref string returnMessage);

        [OperationContract]
        MetroContract_CommunityLinks GetCommunityLinks(int animeID);

        [OperationContract]
        List<MetroContract_Anime_Summary> SearchAnime(int jmmuserID, string queryText, int maxRecords);

        [OperationContract]
        Contract_JMMUser AuthenticateUser(string username, string password);

        [OperationContract]
        List<Contract_JMMUser> GetAllUsers();

        [OperationContract]
        List<Contract_AnimeGroup> GetAllGroups(int userID);

        [OperationContract]
        List<MetroContract_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int jmmuserID);

        [OperationContract]
        MetroContract_Anime_Detail GetAnimeDetail(int animeID, int jmmuserID, int maxEpisodeRecords);

        [OperationContract]
        MetroContract_Anime_Summary GetAnimeSummary(int animeID);

        [OperationContract]
        List<MetroContract_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords);

        [OperationContract]
        List<MetroContract_Comment> GetTraktCommentsForAnime(int animeID, int maxRecords);

        [OperationContract]
        List<MetroContract_Comment> GetAniDBRecommendationsForAnime(int animeID, int maxRecords);

        [OperationContract]
        List<MetroContract_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int jmmuserID);

        [OperationContract]
        List<MetroContract_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int jmmuserID);

        [OperationContract]
        List<MetroContract_Anime_Summary> GetAnimeCalendar(int jmmuserID, int startDateSecs, int endDateSecs,
            int maxRecords);

        [OperationContract]
        List<Contract_VideoDetailed> GetFilesForEpisode(int episodeID, int userID);

        [OperationContract]
        Contract_ToggleWatchedStatusOnEpisode_Response ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus, int userID);

        [OperationContract]
        string UpdateAnimeData(int animeID);
    }
}