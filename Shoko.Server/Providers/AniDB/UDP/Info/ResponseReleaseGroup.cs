using System;

namespace Shoko.Server.Providers.AniDB.UDP.Info
{
    public class ResponseReleaseGroup
    {
        public int ID { get; set; }
        // out of 10
        public decimal Rating { get; set; }
        public int Votes { get; set; }
        public int AnimeCount { get; set; }
        public int FileCount { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string IrcChannel { get; set; }
        public string IrcServer { get; set; }
        public string URL { get; set; }
        public string Picture { get; set; }
    }
}
