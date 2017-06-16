using System;
using Shoko.Models;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Models
{
    [Obsolete]
    public class CrossRef_AniDB_TvDB
    {
        public CrossRef_AniDB_TvDB()
        {
        }

        public int CrossRef_AniDB_TvDBID { get; private set; }
        public int AnimeID { get; set; }
        public int TvDBID { get; set; }
        public int TvDBSeasonNumber { get; set; }
        public int CrossRefSource { get; set; }

        public TvDB_Series GetTvDBSeries()
        {
            return RepoFactory.TvDB_Series.GetByTvDBID(TvDBID);
        }
    }
}