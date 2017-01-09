using System;
using System.IO;
using System.Xml;
using NLog;
using Shoko.Models;
using Shoko.Server.ImageDownload;

namespace Shoko.Server.Entities
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

                string fname = Filename;
                fname = Filename.Replace("/", @"\");
                return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
            }
        }

        public TvDB_Episode()
        {
        }

        public void Populate(XmlDocument doc)
        {
            // used when getting information from episode info
            // http://thetvdb.com/api/B178B8940CAF4A2C/episodes/306542/en.xml

            this.Id = int.Parse(TryGetProperty(doc, "id"));
            this.SeriesID = int.Parse(TryGetProperty(doc, "seriesid"));
            this.SeasonID = int.Parse(TryGetProperty(doc, "seasonid"));
            this.SeasonNumber = int.Parse(TryGetProperty(doc, "SeasonNumber"));
            this.EpisodeNumber = int.Parse(TryGetProperty(doc, "EpisodeNumber"));

            int flag = 0;
            if (int.TryParse(TryGetProperty(doc, "EpImgFlag"), out flag))
                this.EpImgFlag = flag;

            int abnum = 0;
            if (int.TryParse(TryGetProperty(doc, "absolute_number"), out abnum))
                this.AbsoluteNumber = abnum;

            this.EpisodeName = TryGetProperty(doc, "EpisodeName");
            this.Overview = TryGetProperty(doc, "Overview");
            this.Filename = TryGetProperty(doc, "filename");
            //this.FirstAired = TryGetProperty(doc, "FirstAired");

            int aas = 0;
            if (int.TryParse(TryGetProperty(doc, "airsafter_season"), out aas))
                this.AirsAfterSeason = aas;
            else
                this.AirsAfterSeason = null;

            int abe = 0;
            if (int.TryParse(TryGetProperty(doc, "airsbefore_episode"), out abe))
                this.AirsBeforeEpisode = abe;
            else
                this.AirsBeforeEpisode = null;

            int abs = 0;
            if (int.TryParse(TryGetProperty(doc, "airsbefore_season"), out abs))
                this.AirsBeforeSeason = abs;
            else
                this.AirsBeforeSeason = null;
        }

        public void Populate(XmlNode node)
        {
            // used when getting information from full series info
            // http://thetvdb.com/api/B178B8940CAF4A2C/series/84187/all/en.xml

            this.Id = int.Parse(TryGetProperty(node, "id"));
            this.SeriesID = int.Parse(TryGetProperty(node, "seriesid"));
            this.SeasonID = int.Parse(TryGetProperty(node, "seasonid"));
            this.SeasonNumber = int.Parse(TryGetProperty(node, "SeasonNumber"));
            this.EpisodeNumber = int.Parse(TryGetProperty(node, "EpisodeNumber"));

            int flag = 0;
            if (int.TryParse(TryGetProperty(node, "EpImgFlag"), out flag))
                this.EpImgFlag = flag;

            int abnum = 0;
            if (int.TryParse(TryGetProperty(node, "absolute_number"), out abnum))
                this.AbsoluteNumber = abnum;

            this.EpisodeName = TryGetProperty(node, "EpisodeName");
            this.Overview = TryGetProperty(node, "Overview");
            this.Filename = TryGetProperty(node, "filename");
            //this.FirstAired = TryGetProperty(node, "FirstAired");

            int aas = 0;
            if (int.TryParse(TryGetProperty(node, "airsafter_season"), out aas))
                this.AirsAfterSeason = aas;
            else
                this.AirsAfterSeason = null;

            int abe = 0;
            if (int.TryParse(TryGetProperty(node, "airsbefore_episode"), out abe))
                this.AirsBeforeEpisode = abe;
            else
                this.AirsBeforeEpisode = null;

            int abs = 0;
            if (int.TryParse(TryGetProperty(node, "airsbefore_season"), out abs))
                this.AirsBeforeSeason = abs;
            else
                this.AirsBeforeSeason = null;
        }

        protected string TryGetProperty(XmlNode node, string propertyName)
        {
            try
            {
                string prop = node[propertyName].InnerText.Trim();
                return prop;
            }
            catch (Exception ex)
            {
                //logger.Error( ex,"Error in TvDB_Episode.TryGetProperty: " + ex.ToString());
            }

            return "";
        }

        protected string TryGetProperty(XmlDocument doc, string propertyName)
        {
            try
            {
                string prop = doc["Data"]["Episode"][propertyName].InnerText.Trim();
                return prop;
            }
            catch (Exception ex)
            {
                //logger.Error( ex,"Error in TvDB_Episode.TryGetProperty: " + ex.ToString());
            }

            return "";
        }

        public Contract_TvDB_Episode ToContract()
        {
            Contract_TvDB_Episode contract = new Contract_TvDB_Episode();
            contract.AbsoluteNumber = this.AbsoluteNumber;
            contract.EpImgFlag = this.EpImgFlag;
            contract.EpisodeName = this.EpisodeName;
            contract.EpisodeNumber = this.EpisodeNumber;
            contract.Filename = this.Filename;
            contract.Id = this.Id;
            contract.Overview = this.Overview;
            contract.SeasonID = this.SeasonID;
            contract.SeasonNumber = this.SeasonNumber;
            contract.SeriesID = this.SeriesID;
            contract.TvDB_EpisodeID = this.TvDB_EpisodeID;

            contract.AirsAfterSeason = this.AirsAfterSeason;
            contract.AirsBeforeEpisode = this.AirsBeforeEpisode;
            contract.AirsBeforeSeason = this.AirsBeforeSeason;

            return contract;
        }
    }
}