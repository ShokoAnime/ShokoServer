using System;
using System.Text;
using AniDBAPI;
using JMMServer.Repositories;
using NHibernate;
using Shoko.Models.Server;

namespace JMMServer.Entities
{
    public class SVR_AniDB_Episode : AniDB_Episode
    {

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("episodeID: " + EpisodeID.ToString());
            sb.Append(" | animeID: " + AnimeID.ToString());
            sb.Append(" | episodeNumber: " + EpisodeNumber.ToString());
            sb.Append(" | episodeType: " + EpisodeType);
            sb.Append(" | englishName: " + EnglishName);
            sb.Append(" | airDate: " + AirDate);
            //sb.Append(" | AirDateFormatted: " + AirDateFormatted);

            return sb.ToString();
        }

        public void Populate(Raw_AniDB_Episode epInfo)
        {
            this.AirDate = epInfo.AirDate;
            this.AnimeID = epInfo.AnimeID;
            this.DateTimeUpdated = DateTime.Now;
            this.EnglishName = epInfo.EnglishName;
            this.EpisodeID = epInfo.EpisodeID;
            this.EpisodeNumber = epInfo.EpisodeNumber;
            this.EpisodeType = epInfo.EpisodeType;
            this.LengthSeconds = epInfo.LengthSeconds;
            this.Rating = epInfo.Rating.ToString();
            this.RomajiName = epInfo.RomajiName;
            this.Votes = epInfo.Votes.ToString();
        }


        public void CreateAnimeEpisode(ISession session, int animeSeriesID)
        {
            // check if there is an existing episode for this EpisodeID
            SVR_AnimeEpisode existingEp = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(EpisodeID);

            if (existingEp == null)
            {
                SVR_AnimeEpisode animeEp = new SVR_AnimeEpisode();
                animeEp.Populate(this);
                animeEp.AnimeSeriesID = animeSeriesID;
                RepoFactory.AnimeEpisode.Save(animeEp);
            }
        }

    }
}