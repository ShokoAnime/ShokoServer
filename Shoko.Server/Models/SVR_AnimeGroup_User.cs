using Shoko.Models.Server;

namespace Shoko.Server.Models;

public class SVR_AnimeGroup_User : AnimeGroup_User
{
    public SVR_AnimeGroup_User()
    {
    }

    public SVR_AnimeGroup_User(int userID, int groupID)
    {
        JMMUserID = userID;
        AnimeGroupID = groupID;
        IsFave = 0;
        UnwatchedEpisodeCount = 0;
        WatchedEpisodeCount = 0;
        WatchedDate = null;
        PlayedCount = 0;
        WatchedCount = 0;
        StoppedCount = 0;
    }
}
