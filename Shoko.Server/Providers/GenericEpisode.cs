using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Models.Server;
using Shoko.Server.Repositories;
using TMDbLib.Objects.TvShows;

namespace Shoko.Server.Providers
{
    //mpiva: Helper class, in the future this should be an interface that every Episode entity derives, currently exists so we don't need to change the database
    public class GenericEpisode
    {
        public string ProviderId { get; set; }
        public string Provider { get; set; }
        public string ProviderEpisodeId { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string Title { get; set; }
        public DateTime? AirDate { get; set; }

        public IEpisodeGenericRepo Repo { get; private set; }

        public GenericEpisode(string provider)
        {
            Repo = RepoFromProvider(provider);
        }

        public GenericEpisode(TvGroupEpisode episode)
        {
            ProviderId = episode.ShowId.ToString();
            if (episode.Id == null)
                ProviderEpisodeId = episode.ShowId.ToString() + "#" + episode.SeasonNumber.ToString() + "#" + episode.EpisodeNumber.ToString();
            else
                ProviderEpisodeId = episode.ShowId.ToString()+"_"+episode.Id.ToString();
            EpisodeNumber = episode.EpisodeNumber;
            SeasonNumber = episode.SeasonNumber;
            Title = episode.Name;
            AirDate = episode.AirDate;
            Provider = Shoko.Models.Constants.Providers.MovieDBSeries;
            Repo = RepoFromProvider(Provider);
        }
        public GenericEpisode(TvDB_Episode episode)
        {
            ProviderId = episode.SeriesID.ToString();
            ProviderEpisodeId = episode.Id.ToString();
            EpisodeNumber = episode.EpisodeNumber;
            SeasonNumber = episode.SeasonNumber;
            Title = episode.EpisodeName;
            AirDate = episode.AirDate;
            Provider = Shoko.Models.Constants.Providers.TvDB;
            Repo = RepoFromProvider(Provider);
        }

        public GenericEpisode(Trakt_Episode episode)
        {
            ProviderId = episode.Trakt_ShowID.ToString();
            ProviderEpisodeId = ProviderId + "_" + SeasonNumber + "_" + EpisodeNumber;
            EpisodeNumber = episode.EpisodeNumber;
            SeasonNumber = episode.Season;
            Title = episode.Title;
            AirDate = null;
            Provider = Shoko.Models.Constants.Providers.Trakt;
            Repo = RepoFromProvider(Provider);
        }

        public static IEpisodeGenericRepo RepoFromProvider(string provider)
        {
            switch (provider)
            {
                case Shoko.Models.Constants.Providers.TvDB:
                    return RepoFactory.TvDB_Episode;
                case Shoko.Models.Constants.Providers.Trakt:
                    return RepoFactory.Trakt_Episode;
                case Shoko.Models.Constants.Providers.MovieDB:
                    return RepoFactory.MovieDb_Series;
            }

            return null;
        }


        //TODO: mpiva, Clean up with mess with a Common interface
        public (int season, int episodeNumber) GetNextEpisode()
        {

            int epsInSeason = Repo.GetNumberOfEpisodesForSeason(ProviderId, SeasonNumber);
            if (EpisodeNumber == epsInSeason)
            {
                int numberOfSeasons = Repo.GetLastSeasonForSeries(ProviderId);
                if (SeasonNumber == numberOfSeasons) return (0, 0);
                return (SeasonNumber + 1, 1);
            }
            return (SeasonNumber, EpisodeNumber + 1);
        }

        public (int season, int episodeNumber) GetPreviousEpisode()
        {
            // check bounds and exit
            if (SeasonNumber == 1 && EpisodeNumber == 1) return (0, 0);
            // self explanatory
            if (EpisodeNumber > 1) return (SeasonNumber, EpisodeNumber - 1);
            // episode number is 1
            // get the last episode of last season
            int epsInSeason = Repo.GetNumberOfEpisodesForSeason(ProviderId, SeasonNumber - 1);
            return (SeasonNumber - 1, epsInSeason);
        }


        public int GetAbsoluteEpisodeNumber()
        {
            if (SeasonNumber == 1 || SeasonNumber == 0) return EpisodeNumber;
            int number = EpisodeNumber;
            int maxseason = Repo.GetLastSeasonForSeries(ProviderId);
            for (int season = 1; season < maxseason; season++)
            {
                number += Repo.GetNumberOfEpisodesForSeason(ProviderId, season);
            }
            return number;
        }
    }
}
