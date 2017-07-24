using System.Collections.Generic;

namespace Shoko.Server.AniDB_API
{
    public class UpdatesCollection
    {
        protected UpdatesCollection()
        {
            rawAnimeIDs = "";
            updateCount = 0;
        }

        public string rawAnimeIDs { get; set; }

        public long updateCount { get; set; }

        public List<int> AnimeIDs
        {
            get
            {
                List<int> ids = new List<int>();
                string[] sids = rawAnimeIDs.Split('|');
                foreach (string sid in sids)
                {
                    if (int.TryParse(sid, out int id)) ids.Add(id);
                }

                return ids;
            }
        }
    }
}