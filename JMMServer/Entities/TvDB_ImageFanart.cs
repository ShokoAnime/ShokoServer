using System;
using System.IO;
using System.Xml;
using JMMContracts;
using JMMServer.ImageDownload;
using NLog;

namespace JMMServer.Entities
{
    public class TvDB_ImageFanart
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public int TvDB_ImageFanartID { get; private set; }
        public int Id { get; set; }
        public int SeriesID { get; set; }
        public string BannerPath { get; set; }
        public string BannerType { get; set; }
        public string BannerType2 { get; set; }
        public string Colors { get; set; }
        public string Language { get; set; }
        public string ThumbnailPath { get; set; }
        public string VignettePath { get; set; }
        public int Enabled { get; set; }
        public int Chosen { get; set; }

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

        public string FullThumbnailPath
        {
            get
            {
                var fname = ThumbnailPath;
                fname = ThumbnailPath.Replace("/", @"\");
                return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
            }
        }

        public Contract_TvDB_ImageFanart ToContract()
        {
            var contract = new Contract_TvDB_ImageFanart();
            contract.TvDB_ImageFanartID = TvDB_ImageFanartID;
            contract.Id = Id;
            contract.SeriesID = SeriesID;
            contract.BannerPath = BannerPath;
            contract.BannerType = BannerType;
            contract.BannerType2 = BannerType2;
            contract.Colors = Colors;
            contract.Language = Language;
            contract.ThumbnailPath = ThumbnailPath;
            contract.VignettePath = VignettePath;
            contract.Enabled = Enabled;
            contract.Chosen = Chosen;

            return contract;
        }

        public bool Populate(int seriesID, XmlNode node)
        {
            try
            {
                SeriesID = seriesID;
                Id = int.Parse(node["id"].InnerText);
                BannerPath = node["BannerPath"].InnerText;
                BannerType = node["BannerType"].InnerText;
                BannerType2 = node["BannerType2"].InnerText;
                Colors = node["Colors"].InnerText;
                Language = node["Language"].InnerText;
                ThumbnailPath = node["ThumbnailPath"].InnerText;
                VignettePath = node["VignettePath"].InnerText;
                return true;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in TvDB_ImageFanart.Init: " + ex, ex);
                return false;
            }
        }

        public void Valid()
        {
            if (!File.Exists(FullImagePath) || !File.Exists(FullThumbnailPath))
            {
                //clean leftovers
                if (File.Exists(FullImagePath))
                {
                    File.Delete(FullImagePath);
                }
                if (File.Exists(FullThumbnailPath))
                {
                    File.Delete(FullThumbnailPath);
                }
            }
        }
    }
}