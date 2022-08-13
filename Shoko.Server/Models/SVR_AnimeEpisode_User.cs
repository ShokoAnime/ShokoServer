using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_AnimeEpisode_User : AnimeEpisode_User
    {
        public SVR_AnimeEpisode_User() {}

        public SVR_AnimeEpisode_User(int userID, int episodeID, int seriesID)
        {
            JMMUserID = userID;
            AnimeEpisodeID = episodeID;
            AnimeSeriesID = seriesID;
            PlayedCount = 0;
            StoppedCount = 0;
            WatchedCount = 0;
            WatchedDate = null;
        }

        public SVR_AnimeEpisode GetAnimeEpisode()
        {
            return RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID);
        }
    }
}