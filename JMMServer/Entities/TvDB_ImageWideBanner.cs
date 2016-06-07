using System;
using System.IO;
using System.Xml;
using JMMContracts;
using JMMServer.ImageDownload;
using NLog;

namespace JMMServer.Entities
{
    public class TvDB_ImageWideBanner
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public int TvDB_ImageWideBannerID { get; private set; }
        public int Id { get; set; }
        public int SeriesID { get; set; }
        public string BannerPath { get; set; }
        public string BannerType { get; set; }
        public string BannerType2 { get; set; }
        public string Language { get; set; }
        public int Enabled { get; set; }
        public int? SeasonNumber { get; set; }

        public string FullImagePath
        {
            get
            {
                if (string.IsNullOrEmpty(BannerPath)) return "";

                var fname = BannerPath;
                fname = BannerPath.Replace("/", @"\");
                return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
            }
        }

        public Contract_TvDB_ImageWideBanner ToContract()
        {
            var contract = new Contract_TvDB_ImageWideBanner();
            contract.TvDB_ImageWideBannerID = TvDB_ImageWideBannerID;
            contract.Id = Id;
            contract.SeriesID = SeriesID;
            contract.BannerPath = BannerPath;
            contract.BannerType = BannerType;
            contract.BannerType2 = BannerType2;
            contract.Language = Language;
            contract.Enabled = Enabled;
            contract.SeasonNumber = SeasonNumber;

            return contract;
        }

        public bool Populate(int seriesID, XmlNode node, TvDBImageNodeType nodeType)
        {
            try
            {
                SeriesID = seriesID;

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
                logger.ErrorException("Error in TvDB_ImageWideBanner.Populate: " + ex, ex);
                return false;
            }
        }
    }
}