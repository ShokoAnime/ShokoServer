using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_AnimeSeries_User : AnimeSeries_User
{
    public SVR_AnimeSeries_User()
    {
    }

    public SVR_AnimeSeries_User(int userID, int seriesID)
    {
        JMMUserID = userID;
        AnimeSeriesID = seriesID;
        UnwatchedEpisodeCount = 0;
        WatchedEpisodeCount = 0;
        WatchedDate = null;
        PlayedCount = 0;
        WatchedCount = 0;
        StoppedCount = 0;
        LastEpisodeUpdate = null;
    }

    public virtual SVR_AnimeSeries AnimeSeries
        => RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);
}
