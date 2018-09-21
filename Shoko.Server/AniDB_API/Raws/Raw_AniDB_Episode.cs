using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Shoko.Commons.Utils;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Episode : XMLBase
    {

        public int EpisodeID { get; set; }

        public int VersionNumber { get; set; }

        public int AnimeID { get; set; }

        public int LengthSeconds { get; set; }

        public decimal Rating { get; set; }

        public int Votes { get; set; }

        public int EpisodeNumber { get; set; }

        public int EpisodeType { get; set; }

        public string Description { get; set; }

        public int AirDate { get; set; }

        public DateTime DateTimeUpdated { get; set; }

        public int IsDoubleEpisode { get; set; }

        [JsonIgnore, XmlIgnore]
        public bool IsValid { get; private set; }

        public Raw_AniDB_Episode()
        {
            VersionNumber = 0;
            LengthSeconds = 0;
            Rating = 0;
            Votes = 0;
            EpisodeNumber = 0;
            EpisodeType = 1;
            Description = string.Empty;
            AirDate = 0;
            DateTimeUpdated = DateTime.Now;
            IsDoubleEpisode = 0;
            IsValid = false;
        }

        public bool ProcessEpisodeSource(XmlNode node, int anid)
        {
            if (string.IsNullOrEmpty(node?.Attributes?["id"]?.Value)) return false;
            if (!int.TryParse(node?.Attributes?["id"]?.Value, out int id)) return false;
            EpisodeID = id;
            // default values
            LengthSeconds = 0;
            Rating = 0;
            Votes = 0;
            EpisodeNumber = 0;
            EpisodeType = 1;
            AirDate = 0;
            DateTimeUpdated = DateTime.Now;

            AnimeID = anid;

            string epno = AniDBHTTPHelper.TryGetProperty(node, "epno");
            EpisodeType = GetEpisodeType(epno);
            EpisodeNumber = GetEpisodeNumber(epno);
            IsDoubleEpisode = GetIsDoubleEpisode(epno);

            string length = AniDBHTTPHelper.TryGetProperty(node, "length");
            int.TryParse(length, out int lMinutes);
            int secs = lMinutes * 60;
            LengthSeconds = secs;

            NumberStyles style = NumberStyles.Number;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

            if (decimal.TryParse(AniDBHTTPHelper.TryGetProperty(node, "rating"), style, culture, out decimal rating))
                Rating = rating;
            if (int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "rating", "votes"), out int votes))
                Votes = votes;

            // Titles
            foreach (XmlNode nodeChild in node.ChildNodes)
            {
                // Make sure that it's a title and has data
                if (!(nodeChild?.Name.Equals("title") ?? false) ||
                    string.IsNullOrEmpty(nodeChild.InnerText)) continue;

                // get language
                string language = nodeChild.Attributes?["xml:lang"]?.Value?.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(language)) continue;
                string title = nodeChild.InnerText.Trim().Replace('`', '\'');

                using (var upd = Repo.Instance.AniDB_Episode_Title.BeginAddOrUpdate(() => Repo.Instance.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(id, language).FirstOrDefault()))
                {
                    upd.Entity.AniDB_EpisodeID = id;
                    upd.Entity.Language = language;
                    upd.Entity.Title = title;
                    upd.Commit();
                }
            }

            string adate = AniDBHTTPHelper.TryGetProperty(node, "airdate");

            AirDate = AniDB.GetAniDBDateAsSeconds(adate, true);

            Description = AniDBHTTPHelper.TryGetProperty(node, "summary")?.Replace('`', '\'');
            IsValid = true;
            return true;
        }

        private int GetEpisodeNumber(string fld)
        {
            int epType = GetEpisodeType(fld);

            // if it is NOT a normal episode strip the leading character
            string fldTemp = fld;
            if (epType > 1)
                fldTemp = fld.Trim().Substring(1, fld.Trim().Length - 1);

            if (!int.TryParse(fldTemp, out int epno))
            {
                // if we couldn't convert to an int, it must mean it is a double episode
                // we will just take the first ep as the episode number
                string[] sDetails = fldTemp.Split('-');
                epno = int.Parse(sDetails[0]);
            }
            return epno;
        }

        private int GetIsDoubleEpisode(string fld)
        {
            //BaseConfig.MyAnimeLog.Write("GetIsDoubleEpisode: {0}", fld);

            int epType = GetEpisodeType(fld);

            // if it is NOT a normal episode strip the leading character
            string fldTemp = fld;
            if (epType > 1)
                fldTemp = fld.Trim().Substring(1, fld.Trim().Length - 1);

            return int.TryParse(fldTemp, out int _) ? 0 : 1;
        }

        private int GetEpisodeType(string fld)
        {
            // if the first char is a numeric than it is a normal episode
            if (int.TryParse(fld.Trim().Substring(0, 1), out _))
                return (int)Shoko.Models.Enums.EpisodeType.Episode;
            // the first character should contain the type of special episode
            // S(special), C(credits), T(trailer), P(parody), O(other)
            // we will just take this and store it in the database
            // this will allow for the user customizing how it is displayed on screen later
            var epType = fld.Trim().Substring(0, 1).ToUpper();

            switch (epType)
            {
                case "":
                    return (int)Shoko.Models.Enums.EpisodeType.Episode;
                case "C":
                    return (int)Shoko.Models.Enums.EpisodeType.Credits;
                case "S":
                    return (int)Shoko.Models.Enums.EpisodeType.Special;
                case "O":
                    return (int)Shoko.Models.Enums.EpisodeType.Other;
                case "T":
                    return (int)Shoko.Models.Enums.EpisodeType.Trailer;
                case "P":
                    return (int)Shoko.Models.Enums.EpisodeType.Parody;
                default:
                    return (int)Shoko.Models.Enums.EpisodeType.Episode;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("episodeID: " + EpisodeID);
            sb.Append(" | animeID: " + AnimeID);
            sb.Append(" | episodeNumber: " + EpisodeNumber);
            sb.Append(" | episodeType: " + EpisodeType);
            sb.Append(" | airDate: " + AirDate);

            return sb.ToString();
        }
    }
}
