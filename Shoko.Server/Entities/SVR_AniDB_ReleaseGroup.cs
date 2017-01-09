using AniDBAPI;
using Shoko.Models.Server;

namespace Shoko.Server.Entities
{
    public class SVR_AniDB_ReleaseGroup : AniDB_ReleaseGroup
    {
        
        public SVR_AniDB_ReleaseGroup()
        {
        }

        public SVR_AniDB_ReleaseGroup(Raw_AniDB_Group raw)
        {
            Populate(raw);
        }

        public void Populate(Raw_AniDB_Group raw)
        {
            this.GroupID = raw.GroupID;
            this.Rating = raw.Rating;
            this.Votes = raw.Votes;
            this.AnimeCount = raw.AnimeCount;
            this.FileCount = raw.FileCount;
            this.GroupName = raw.GroupName;
            this.GroupNameShort = raw.GroupNameShort;
            this.IRCChannel = raw.IRCChannel;
            this.IRCServer = raw.IRCServer;
            this.URL = raw.URL;
            this.Picname = raw.Picname;
        }

        public override string ToString()
        {
            return string.Format("Release Group: {0} - {1} : {2}", GroupID, GroupName, URL);
        }
    }
}