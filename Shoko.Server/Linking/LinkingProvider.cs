using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Providers.TvDB
{
    public class LinkingProvider
    {
        public Func<string, List<LinkingEpisode>> GetAll { get; }
        public Func<string, int, int> GetNumberOfEpisodesForSeason { get; }
        public Func<string, int> GetLastSeasonForSeries { get; }
        public LinkingProvider(CrossRefType cr)
        {
            if (cr == CrossRefType.TvDB)
            {
                GetAll = (ser) => Repo.Instance.TvDB_Episode.GetBySeriesID(int.Parse(ser)).Select(a => new LinkingEpisode(a,this)).ToList();
                GetNumberOfEpisodesForSeason = (ser,season) => Repo.Instance.TvDB_Episode.GetNumberOfEpisodesForSeason(int.Parse(ser), season);
                GetLastSeasonForSeries = (ser) => Repo.Instance.TvDB_Episode.GetLastSeasonForSeries(int.Parse(ser));

            }
            else if (cr == CrossRefType.TraktTV)
            {
                GetAll= (ser) => Repo.Instance.Trakt_Episode.GetByShowID(int.Parse(ser)).Select(a => new LinkingEpisode(a, this)).ToList();
                GetNumberOfEpisodesForSeason = (ser, season) => Repo.Instance.Trakt_Episode.GetNumberOfEpisodesForSeason(int.Parse(ser), season);
                GetLastSeasonForSeries = (ser) => Repo.Instance.Trakt_Episode.GetLastSeasonForSeries(int.Parse(ser));
            }
        }
    }
}