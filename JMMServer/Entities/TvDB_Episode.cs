using System;
using System.IO;
using System.Xml;
using JMMContracts;
using JMMServer.ImageDownload;
using NLog;

namespace JMMServer.Entities
{
    public class TvDB_Episode
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int TvDB_EpisodeID { get; private set; }
        public int Id { get; set; }
        public int SeriesID { get; set; }
        public int SeasonID { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string EpisodeName { get; set; }
        public string Overview { get; set; }
        public string Filename { get; set; }
        public int EpImgFlag { get; set; }
        public int? AbsoluteNumber { get; set; }
        public int? AirsAfterSeason { get; set; }
        public int? AirsBeforeEpisode { get; set; }
        public int? AirsBeforeSeason { get; set; }

        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrEmpty(Filename)) return "";

                var fname = Filename;
                fname = Filename.Replace("/", @"\");
                return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
            }
        }

        public void Populate(XmlDocument doc)
        {
            // used when getting information from episode info
            // http://thetvdb.com/api/B178B8940CAF4A2C/episodes/306542/en.xml

            Id = int.Parse(TryGetProperty(doc, "id"));
            SeriesID = int.Parse(TryGetProperty(doc, "seriesid"));
            SeasonID = int.Parse(TryGetProperty(doc, "seasonid"));
            SeasonNumber = int.Parse(TryGetProperty(doc, "SeasonNumber"));
            EpisodeNumber = int.Parse(TryGetProperty(doc, "EpisodeNumber"));

            var flag = 0;
            if (int.TryParse(TryGetProperty(doc, "EpImgFlag"), out flag))
                EpImgFlag = flag;

            var abnum = 0;
            if (int.TryParse(TryGetProperty(doc, "absolute_number"), out abnum))
                AbsoluteNumber = abnum;

            EpisodeName = TryGetProperty(doc, "EpisodeName");
            Overview = TryGetProperty(doc, "Overview");
            Filename = TryGetProperty(doc, "filename");
            //this.FirstAired = TryGetProperty(doc, "FirstAired");

            var aas = 0;
            if (int.TryParse(TryGetProperty(doc, "airsafter_season"), out aas))
                AirsAfterSeason = aas;
            else
                AirsAfterSeason = null;

            var abe = 0;
            if (int.TryParse(TryGetProperty(doc, "airsbefore_episode"), out abe))
                AirsBeforeEpisode = abe;
            else
                AirsBeforeEpisode = null;

            var abs = 0;
            if (int.TryParse(TryGetProperty(doc, "airsbefore_season"), out abs))
                AirsBeforeSeason = abs;
            else
                AirsBeforeSeason = null;
        }

        public void Populate(XmlNode node)
        {
            // used when getting information from full series info
            // http://thetvdb.com/api/B178B8940CAF4A2C/series/84187/all/en.xml

            Id = int.Parse(TryGetProperty(node, "id"));
            SeriesID = int.Parse(TryGetProperty(node, "seriesid"));
            SeasonID = int.Parse(TryGetProperty(node, "seasonid"));
            SeasonNumber = int.Parse(TryGetProperty(node, "SeasonNumber"));
            EpisodeNumber = int.Parse(TryGetProperty(node, "EpisodeNumber"));

            var flag = 0;
            if (int.TryParse(TryGetProperty(node, "EpImgFlag"), out flag))
                EpImgFlag = flag;

            var abnum = 0;
            if (int.TryParse(TryGetProperty(node, "absolute_number"), out abnum))
                AbsoluteNumber = abnum;

            EpisodeName = TryGetProperty(node, "EpisodeName");
            Overview = TryGetProperty(node, "Overview");
            Filename = TryGetProperty(node, "filename");
            //this.FirstAired = TryGetProperty(node, "FirstAired");

            var aas = 0;
            if (int.TryParse(TryGetProperty(node, "airsafter_season"), out aas))
                AirsAfterSeason = aas;
            else
                AirsAfterSeason = null;

            var abe = 0;
            if (int.TryParse(TryGetProperty(node, "airsbefore_episode"), out abe))
                AirsBeforeEpisode = abe;
            else
                AirsBeforeEpisode = null;

            var abs = 0;
            if (int.TryParse(TryGetProperty(node, "airsbefore_season"), out abs))
                AirsBeforeSeason = abs;
            else
                AirsBeforeSeason = null;
        }

        protected string TryGetProperty(XmlNode node, string propertyName)
        {
            try
            {
                var prop = node[propertyName].InnerText.Trim();
                return prop;
            }
            catch (Exception ex)
            {
                //logger.ErrorException("Error in TvDB_Episode.TryGetProperty: " + ex.ToString(), ex);
            }

            return "";
        }

        protected string TryGetProperty(XmlDocument doc, string propertyName)
        {
            try
            {
                var prop = doc["Data"]["Episode"][propertyName].InnerText.Trim();
                return prop;
            }
            catch (Exception ex)
            {
                //logger.ErrorException("Error in TvDB_Episode.TryGetProperty: " + ex.ToString(), ex);
            }

            return "";
        }

        public Contract_TvDB_Episode ToContract()
        {
            var contract = new Contract_TvDB_Episode();
            contract.AbsoluteNumber = AbsoluteNumber;
            contract.EpImgFlag = EpImgFlag;
            contract.EpisodeName = EpisodeName;
            contract.EpisodeNumber = EpisodeNumber;
            contract.Filename = Filename;
            contract.Id = Id;
            contract.Overview = Overview;
            contract.SeasonID = SeasonID;
            contract.SeasonNumber = SeasonNumber;
            contract.SeriesID = SeriesID;
            contract.TvDB_EpisodeID = TvDB_EpisodeID;

            contract.AirsAfterSeason = AirsAfterSeason;
            contract.AirsBeforeEpisode = AirsBeforeEpisode;
            contract.AirsBeforeSeason = AirsBeforeSeason;

            return contract;
        }
    }
}