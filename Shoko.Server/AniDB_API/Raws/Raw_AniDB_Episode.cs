using System;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Shoko.Server;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Episode : XMLBase
    {
        /*
		
		[XmlIgnore]
		public string AirDateFormatted
		{
			get { return APIUtils.GetAniDBDate(AirDate, System.Globalization.CultureInfo.CurrentCulture); }
		}
        */

        public int EpisodeID { get; set; }

        public int VersionNumber { get; set; }

        public int AnimeID { get; set; }

        public int LengthSeconds { get; set; }

        public decimal Rating { get; set; }

        public int Votes { get; set; }

        public int EpisodeNumber { get; set; }

        public int EpisodeType { get; set; }

        public string RomajiName { get; set; }

        public string KanjiName { get; set; }

        public string EnglishName { get; set; }

        public int AirDate { get; set; }

        public DateTime DateTimeUpdated { get; set; }

        public int IsDoubleEpisode { get; set; }

        [ScriptIgnore, JsonIgnore, XmlIgnore]
        public bool IsValid { get; private set; }

        public Raw_AniDB_Episode()
        {
            VersionNumber = 0;
            LengthSeconds = 0;
            Rating = 0;
            Votes = 0;
            EpisodeNumber = 0;
            EpisodeType = 1;
            RomajiName = string.Empty;
            KanjiName = string.Empty;
            EnglishName = string.Empty;
            AirDate = 0;
            DateTimeUpdated = DateTime.Now;
            IsDoubleEpisode = 0;
            IsValid = false;
        }


        private void ProcessEpisodeSource(string sRecMessage)
        {
            // remove the header info
            string[] sDetails = sRecMessage.Substring(12).Split('|');

            //BaseConfig.MyAnimeLog.Write("PROCESSING EPISODE: {0}", sDetails.Length);

            // 240 EPISODE
            // 0. 99294 ** episode id
            // 1. 6107 ** anime id
            // 2. 25 ** length in minutes
            // 3. 712 ** episode rating (7.12)
            // 4. 14 ** episode vote count
            // 5. 02 ** episode number    Returned 'epno' includes special character (only if special) and padding (only if normal). Special characters are S(special), C(credits), T(trailer), P(parody), O(other)
            // 6. The Day It Began ** english name
            // 7. Hajimari no Hi ** romaji name
            // 8. ?????? ** kanji name
            // 9. 1239494400 ** air date

            if (!int.TryParse(sDetails[0].Trim(), out int epid)) return;
            EpisodeID = epid;
            if (!int.TryParse(sDetails[1].Trim(), out int animeid)) return;
            AnimeID = animeid;

            int.TryParse(sDetails[2].Trim(), out int lMinutes);
            int secs = lMinutes * 60;
            LengthSeconds = secs;

            string epno = GetValidatedEpisodeNumber(sDetails[5].Trim());

            EpisodeType = GetEpisodeType(epno);
            EpisodeNumber = GetEpisodeNumber(epno);
            IsDoubleEpisode = GetIsDoubleEpisode(epno);
            Rating = AniDBAPILib.ProcessAniDBInt(sDetails[3].Trim());
            Votes = AniDBAPILib.ProcessAniDBInt(sDetails[4].Trim());
            EnglishName = AniDBAPILib.ProcessAniDBString(sDetails[6].Trim())?.Replace('`', '\'');
            RomajiName = AniDBAPILib.ProcessAniDBString(sDetails[7].Trim())?.Replace('`', '\'');
            KanjiName = AniDBAPILib.ProcessAniDBString(sDetails[8].Trim())?.Replace('`', '\'');
            AirDate = AniDBAPILib.ProcessAniDBInt(sDetails[9].Trim());
            //BaseConfig.MyAnimeLog.Write("EPISODE: {0}: {1}", sDetails[5].Trim(), this.ToString());
            IsValid = true;
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
            RomajiName = string.Empty;
            KanjiName = string.Empty;
            EnglishName = string.Empty;
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
            EnglishName = AniDBHTTPHelper.TryGetPropertyWithAttribute(node, "title", "xml:lang", "en")?.Replace('`', '\'');
            RomajiName = AniDBHTTPHelper.TryGetPropertyWithAttribute(node, "title", "xml:lang", "x-jat")?.Replace('`', '\'');
            KanjiName = AniDBHTTPHelper.TryGetPropertyWithAttribute(node, "title", "xml:lang", "ja")?.Replace('`', '\'');

            string adate = AniDBHTTPHelper.TryGetProperty(node, "airdate");

            AirDate = AniDB.GetAniDBDateAsSeconds(adate, true);
            IsValid = true;
            return true;
        }

        private void ProcessFileSource(string sRecMessage)
        {
            // remove the header info
            string[] sDetails = sRecMessage.Substring(9).Split('|');

            //BaseConfig.MyAnimeLog.Write("PROCESSING EPISODE: {0}", sDetails.Length);

            // 220 FILE
            // 0. 572794 ** fileid
            // 1. 6107 ** anime id
            // 2. 99294 ** episode id
            // 3. 12 ** group id
            // 4. 2723 ** lid
            // 5. c646d82a184a33f4e4f98af39f29a044 ** ed2k hash
            // 6. 8452c4bf ** crc32
            // 7. high ** quality
            // 8. HDTV ** source
            // 9. Vorbis (Ogg Vorbis) ** audio codec
            // 10. 148 ** audio bit rate
            // 11. H264/AVC ** video codec
            // 12. 1773 ** video bit rate
            // 13. 1280x720 ** video res
            // 14. mkv ** file extension
            // 15. 1470 ** length in seconds
            // 16.   ** description
            // 17. 1239494400 ** release date ** date is the time of the event (in seconds since 1.1.1970) 
            // 18. 2 ** episode #
            // 19. The Day It Began ** ep name 
            // 20. Hajimari no Hi ** ep name romaji20 . 
            // 21. ** ep Kanji Name
            // 22. 712 ** episode rating (7.12)
            // 23. 14 ** episode vote count
            // 24. Eclipse Productions ** group name
            // 25. Eclipse ** group name short

            if (!int.TryParse(sDetails[2].Trim(), out int epid)) return;
            EpisodeID = epid;
            if (!int.TryParse(sDetails[1].Trim(), out int animeid)) return;
            AnimeID = animeid;

            int.TryParse(sDetails[15].Trim(), out int lMinutes);
            int secs = lMinutes * 60;
            LengthSeconds = secs;

            string epno = GetValidatedEpisodeNumber(sDetails[18].Trim());

            EpisodeType = GetEpisodeType(epno);
            EpisodeNumber = GetEpisodeNumber(epno);
            IsDoubleEpisode = GetIsDoubleEpisode(epno);
            Rating = AniDBAPILib.ProcessAniDBInt(sDetails[22].Trim());
            Votes = AniDBAPILib.ProcessAniDBInt(sDetails[23].Trim());
            EnglishName = AniDBAPILib.ProcessAniDBString(sDetails[19].Trim())?.Replace('`', '\'');
            RomajiName = AniDBAPILib.ProcessAniDBString(sDetails[20].Trim())?.Replace('`', '\'');
            KanjiName = AniDBAPILib.ProcessAniDBString(sDetails[21].Trim())?.Replace('`', '\'');
            AirDate = AniDBAPILib.ProcessAniDBInt(sDetails[17].Trim());

            //BaseConfig.MyAnimeLog.Write("EPISODE: {0}: {1}", sDetails[18].Trim(), this.ToString());
            IsValid = true;
            return;
        }

        private string GetValidatedEpisodeNumber(string fld)
        {
            // remove any invalid characters
            // string socketResponse = "220 FILE226237|3651|48951|3|63249613|feaf5388f7c0c5a38cd8d5e243c2c6e7|de3f16d8|high|DTV|MP3 CBR|128|XviD|894|640x360|avi|1420||1145836800|04,07|The Boredom of Suzumiya Haruhi|Suzumiya Haruhi no Taikutsu|x|802|48|a.f.k.|a.f.k.";
            // "04,07"

            if (fld.Contains(","))
            {
                int pos = fld.IndexOf(',');
                fld = fld.Substring(0, pos);
            }

            return fld;
        }

        private int GetEpisodeNumber(string fld)
        {
            //BaseConfig.MyAnimeLog.Write("GetEpisodeNumber: {0}", fld);

            int epType = GetEpisodeType(fld);

            // if it is NOT a normal episode strip the leading character
            string fldTemp = fld;
            if (epType > 1)
                fldTemp = fld.Trim().Substring(1, fld.Trim().Length - 1);

            if (int.TryParse(fldTemp, out int epno))
            {
                return epno;
            }
            else
            {
                // if we couldn't convert to an int, it must mean it is a double episode
                // we will just take the first ep as the episode number
                string[] sDetails = fldTemp.Split('-');
                epno = int.Parse(sDetails[0]);
                return epno;
            }
        }

        private int GetIsDoubleEpisode(string fld)
        {
            //BaseConfig.MyAnimeLog.Write("GetIsDoubleEpisode: {0}", fld);

            int epType = GetEpisodeType(fld);

            // if it is NOT a normal episode strip the leading character
            string fldTemp = fld;
            if (epType > 1)
                fldTemp = fld.Trim().Substring(1, fld.Trim().Length - 1);

            if (int.TryParse(fldTemp, out int epno))
                return 0;
            else
                return 1;
        }

        private int GetEpisodeType(string fld)
        {
            //BaseConfig.MyAnimeLog.Write("GetEpisodeType: {0}", fld);

            string epType = "";
            if (int.TryParse(fld.Trim().Substring(0, 1), out int epno))
            // if the first char is a numeric than it is a normal episode
            {
                return (int)Shoko.Models.Enums.EpisodeType.Episode;
            }
            else
            {
                // the first character should contain the type of special episode
                // S(special), C(credits), T(trailer), P(parody), O(other)
                // we will just take this and store it in the database
                // this will allow for the user customizing how it is displayed on screen later
                epType = fld.Trim().Substring(0, 1).ToUpper();

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
        }

        public Raw_AniDB_Episode(string sRecMessage, EpisodeSourceType sourceType)
        {
            if (sourceType == EpisodeSourceType.Episode)
                ProcessEpisodeSource(sRecMessage);
            else
                ProcessFileSource(sRecMessage);
        }

        /*
		public Raw_AniDB_Episode(XmlNode node, int anid)
		{
			ProcessEpisodeSource(node, anid);
		}*/

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("episodeID: " + EpisodeID.ToString());
            sb.Append(" | animeID: " + AnimeID.ToString());
            sb.Append(" | episodeNumber: " + EpisodeNumber.ToString());
            sb.Append(" | episodeType: " + EpisodeType.ToString());
            sb.Append(" | englishName: " + EnglishName);
            sb.Append(" | airDate: " + AirDate);
            //sb.Append(" | AirDateFormatted: " + AirDateFormatted);

            return sb.ToString();
        }
    }
}