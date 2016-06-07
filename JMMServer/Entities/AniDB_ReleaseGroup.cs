using AniDBAPI;
using JMMContracts;

namespace JMMServer.Entities
{
    public class AniDB_ReleaseGroup
    {
        public AniDB_ReleaseGroup()
        {
        }

        public AniDB_ReleaseGroup(Raw_AniDB_Group raw)
        {
            Populate(raw);
        }

        public int AniDB_ReleaseGroupID { get; private set; }
        public int GroupID { get; set; }
        public int Rating { get; set; }
        public int Votes { get; set; }
        public int AnimeCount { get; set; }
        public int FileCount { get; set; }
        public string GroupName { get; set; }
        public string GroupNameShort { get; set; }
        public string IRCChannel { get; set; }
        public string IRCServer { get; set; }
        public string URL { get; set; }
        public string Picname { get; set; }

        public void Populate(Raw_AniDB_Group raw)
        {
            GroupID = raw.GroupID;
            Rating = raw.Rating;
            Votes = raw.Votes;
            AnimeCount = raw.AnimeCount;
            FileCount = raw.FileCount;
            GroupName = raw.GroupName;
            GroupNameShort = raw.GroupNameShort;
            IRCChannel = raw.IRCChannel;
            IRCServer = raw.IRCServer;
            URL = raw.URL;
            Picname = raw.Picname;
        }

        public Contract_ReleaseGroup ToContract()
        {
            var contract = new Contract_ReleaseGroup();

            contract.GroupID = GroupID;
            contract.Rating = Rating;
            contract.Votes = Votes;
            contract.AnimeCount = AnimeCount;
            contract.FileCount = FileCount;
            contract.GroupName = GroupName;
            contract.GroupNameShort = GroupNameShort;
            contract.IRCChannel = IRCChannel;
            contract.IRCServer = IRCServer;
            contract.URL = URL;
            contract.Picname = Picname;

            return contract;
        }

        public override string ToString()
        {
            return string.Format("Release Group: {0} - {1} : {2}", GroupID, GroupName, URL);
        }
    }
}