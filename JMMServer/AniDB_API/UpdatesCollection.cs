using System.Collections.Generic;

namespace JMMServer.AniDB_API
{
    public class UpdatesCollection
    {
        protected string rawAnimeIDs = "";

        protected long updateCount;


        // default constructor

        public string RawAnimeIDs
        {
            get { return rawAnimeIDs; }
            set { rawAnimeIDs = value; }
        }

        public long UpdateCount
        {
            get { return updateCount; }
            set { updateCount = value; }
        }

        public List<int> AnimeIDs
        {
            get
            {
                var ids = new List<int>();
                var sids = rawAnimeIDs.Split('|');
                foreach (var sid in sids)
                {
                    var id = 0;
                    if (int.TryParse(sid, out id)) ids.Add(id);
                }

                return ids;
            }
        }
    }
}