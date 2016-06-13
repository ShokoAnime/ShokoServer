using System;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Calendar
    {
        #region Properties

        public int AnimeID { get; set; }

        public DateTime ReleaseDate { get; set; }

        public int DateFlags { get; set; }

        public Raw_AniDB_Calendar()
        {
            DateFlags = 0;
        }

        public override string ToString()
        {
            return string.Format("Calendar - AnimeID: {0}...Release Date: {1}...Flags: {2}", AnimeID, ReleaseDate,
                DateFlags);
        }

        #endregion
    }
}