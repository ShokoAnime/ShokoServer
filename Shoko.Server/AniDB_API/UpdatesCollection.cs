using System.Collections.Generic;

namespace Shoko.Server.AniDB_API
{
    public class UpdatesCollection
    {
        protected string rawAnimeIDs = "";

        public string RawAnimeIDs
        {
            get { return rawAnimeIDs; }
            set { rawAnimeIDs = value; }
        }

        protected long updateCount = 0;

        public long UpdateCount
        {
            get { return updateCount; }
            set { updateCount = value; }
        }

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


        // default constructor
        public UpdatesCollection()
        {
        }
    }
}