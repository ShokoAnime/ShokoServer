using System;
using Shoko.Models.Server;

namespace Shoko.Server.Providers.TvDB
{
    public class LinkingEpisode
    {
        public string Id { get; }
        public string Title { get; }
        public int Season { get; set; }
        public int Number { get; set; }
        public string SeriesId { get; set; }
        public DateTime? AirDate { get; }
        public object Original { get; }

        private LinkingProvider _provider;

        public LinkingEpisode(TvDB_Episode e, LinkingProvider prov)
        {
            Id = e.TvDB_EpisodeID.ToString();
            Title = e.EpisodeName;
            Season = e.SeasonNumber;
            Number = e.EpisodeNumber;
            AirDate = e.AirDate;
            SeriesId = e.SeriesID.ToString();
            Original = e;
            _provider = prov;
        }

        public LinkingEpisode(Trakt_Episode e, LinkingProvider prov)
        {
            Id = e.Trakt_EpisodeID.ToString();
            Title = e.Title;
            Season = e.Season;
            Number = e.EpisodeNumber;
            AirDate=null;
            SeriesId=e.Trakt_ShowID.ToString();
            Original = e;
            _provider = prov;
        }

        public (int season, int episodeNumber) GetPreviousEpisode()
        {
            // check bounds and exit
            if (Season == 1 && Number == 1) return (0, 0);
            // self explanatory
            if (Number > 1) return (Season, Number - 1);

            // episode number is 1
            // get the last episode of last season
            int epsInSeason = _provider.GetNumberOfEpisodesForSeason(SeriesId, Season - 1);
            return (Season - 1, epsInSeason);
        }
        public (int season, int episodeNumber) GetNextEpisode()
        {
            int epsInSeason = _provider.GetNumberOfEpisodesForSeason(SeriesId, Season);
            if (Number == epsInSeason)
            {
                int numberOfSeasons = _provider.GetLastSeasonForSeries(SeriesId);
                if (Season == numberOfSeasons) return (0, 0);
                return (Season + 1, 1);
            }

            return (Season, Number + 1);
        }
    }
}
