using System;
using System.IO;
using System.Xml;
using JMMContracts;
using JMMServer.ImageDownload;
using NLog;

namespace JMMServer.Entities
{
    public class TvDB_ImagePoster : IImageEntity
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public int TvDB_ImagePosterID { get; private set; }
        public int Id { get; set; }
        public int SeriesID { get; set; }
        public string BannerPath { get; set; }
        public string BannerType { get; set; }
        public string BannerType2 { get; set; }
        public string Language { get; set; }
        public int Enabled { get; set; }
        public int? SeasonNumber { get; set; }

        public Contract_TvDB_ImagePoster ToContract()
        {
            Contract_TvDB_ImagePoster contract = new Contract_TvDB_ImagePoster();
            contract.TvDB_ImagePosterID = this.TvDB_ImagePosterID;
            contract.Id = this.Id;
            contract.SeriesID = this.SeriesID;
            contract.BannerPath = this.BannerPath;
            contract.BannerType = this.BannerType;
            contract.BannerType2 = this.BannerType2;
            contract.Language = this.Language;
            contract.Enabled = this.Enabled;
            contract.SeasonNumber = this.SeasonNumber;

            return contract;
        }

        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrEmpty(BannerPath)) return "";

                string fname = BannerPath;
                fname = BannerPath.Replace("/", @"\");
                return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
            }
        }

        public bool Populate(int seriesID, XmlNode node, TvDBImageNodeType nodeType)
        {
            try
            {
                this.SeriesID = seriesID;

                if (nodeType == TvDBImageNodeType.Series)
                    SeasonNumber = null;
                else
                    SeasonNumber = int.Parse(node["Season"].InnerText);


                Id = int.Parse(node["id"].InnerText);
                BannerPath = node["BannerPath"].InnerText;
                BannerType = node["BannerType"].InnerText;
                BannerType2 = node["BannerType2"].InnerText;
                Language = node["Language"].InnerText;


                return true;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TvDB_ImagePoster.Populate: " + ex.ToString());
                return false;
            }
        }
    }
}