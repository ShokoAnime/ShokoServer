using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using JMMServer;


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

        public int EpisodeID { get; set;  }

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

			EpisodeID = int.Parse(sDetails[0].Trim());
			AnimeID = int.Parse(sDetails[1].Trim());

			int lMinutes = 0;
			int.TryParse(sDetails[2].Trim(), out lMinutes);
			int secs = lMinutes * 60;
			LengthSeconds = secs;

			string epno = GetValidatedEpisodeNumber(sDetails[5].Trim());

			EpisodeType = GetEpisodeType(epno);
			EpisodeNumber = GetEpisodeNumber(epno);
			IsDoubleEpisode = GetIsDoubleEpisode(epno);
		    Rating = AniDBAPILib.ProcessAniDBInt(sDetails[3].Trim());
		    Votes = AniDBAPILib.ProcessAniDBInt(sDetails[4].Trim());
			EnglishName = AniDBAPILib.ProcessAniDBString(sDetails[6].Trim());
			RomajiName = AniDBAPILib.ProcessAniDBString(sDetails[7].Trim());
            KanjiName = AniDBAPILib.ProcessAniDBString(sDetails[8].Trim());
		    AirDate = AniDBAPILib.ProcessAniDBInt(sDetails[9].Trim());
			//BaseConfig.MyAnimeLog.Write("EPISODE: {0}: {1}", sDetails[5].Trim(), this.ToString());
		}

		public void ProcessEpisodeSource(XmlNode node, int anid)
		{
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

			EpisodeID = int.Parse(node.Attributes["id"].Value);
			AnimeID = anid;

			string epno = AniDBHTTPHelper.TryGetProperty(node, "epno");
			EpisodeType = GetEpisodeType(epno);
			EpisodeNumber = GetEpisodeNumber(epno);
			IsDoubleEpisode = GetIsDoubleEpisode(epno);

			string length = AniDBHTTPHelper.TryGetProperty(node, "length");
			int lMinutes = 0;
			int.TryParse(length, out lMinutes);
			int secs = lMinutes * 60;
		    LengthSeconds = secs;

		    decimal rating = 0;
		    int votes = 0;

			//string airdate = TryGetProperty(node, "airdate");
			decimal.TryParse(AniDBHTTPHelper.TryGetProperty(node, "rating"), out rating);
		    int.TryParse(AniDBHTTPHelper.TryGetAttribute(node, "rating", "votes"), out votes);
		    Rating = rating;
		    Votes = votes;
			EnglishName = AniDBHTTPHelper.TryGetPropertyWithAttribute(node, "title", "xml:lang", "en");
			RomajiName = AniDBHTTPHelper.TryGetPropertyWithAttribute(node, "title", "xml:lang", "x-jat");
			KanjiName = AniDBHTTPHelper.TryGetPropertyWithAttribute(node, "title", "xml:lang", "ja");

			/*
	
			<title xml:lang="en">The Adventures of Asahina Mikuru Episode 00</title>
			<title xml:lang="lt">Mikuru Asahinos nuotykiai Epizodas 00</title>
			<title xml:lang="x-jat">Asahina Mikuru no Bouken Episode 00</title>*/
            string adate = AniDBHTTPHelper.TryGetProperty(node, "airdate");
		   
			AirDate = Utils.GetAniDBDateAsSeconds(adate); 

			//BaseConfig.MyAnimeLog.Write("EPISODE: {0}: {1}", epno.Trim(), this.ToString());
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

			EpisodeID = int.Parse(sDetails[2].Trim());
			AnimeID = int.Parse(sDetails[1].Trim());

			int lMinutes = 0;
			int.TryParse(sDetails[15].Trim(), out lMinutes);
			int secs = lMinutes * 60;
			LengthSeconds = secs;

			string epno = GetValidatedEpisodeNumber(sDetails[18].Trim());

			EpisodeType = GetEpisodeType(epno);
			EpisodeNumber = GetEpisodeNumber(epno);
			IsDoubleEpisode = GetIsDoubleEpisode(epno);
		    Rating = AniDBAPILib.ProcessAniDBInt(sDetails[22].Trim());
		    Votes = AniDBAPILib.ProcessAniDBInt(sDetails[23].Trim());
			EnglishName = AniDBAPILib.ProcessAniDBString(sDetails[19].Trim());
			RomajiName = AniDBAPILib.ProcessAniDBString(sDetails[20].Trim());
            KanjiName = AniDBAPILib.ProcessAniDBString(sDetails[21].Trim());
		    AirDate = AniDBAPILib.ProcessAniDBInt(sDetails[17].Trim());
			
			//BaseConfig.MyAnimeLog.Write("EPISODE: {0}: {1}", sDetails[18].Trim(), this.ToString());
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
			int epno = 0;

			int epType = GetEpisodeType(fld);
			
			// if it is NOT a normal episode strip the leading character
			string fldTemp = fld;
			if (epType > 1)
				fldTemp = fld.Trim().Substring(1, fld.Trim().Length - 1);

			if (int.TryParse(fldTemp, out epno))
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
			int epno = 0;

			int epType = GetEpisodeType(fld);

			// if it is NOT a normal episode strip the leading character
			string fldTemp = fld;
			if (epType > 1)
				fldTemp = fld.Trim().Substring(1, fld.Trim().Length - 1);

			if (int.TryParse(fldTemp, out epno))
				return 0;
			else
				return 1;
		}

		private int GetEpisodeType(string fld)
		{
			//BaseConfig.MyAnimeLog.Write("GetEpisodeType: {0}", fld);

			string epType = ""; 
			int epno = 0;
			if (int.TryParse(fld.Trim().Substring(0, 1), out epno)) // if the first char is a numeric than it is a normal episode
			{
				return (int)enEpisodeType.Episode;
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
					case "": return (int)enEpisodeType.Episode;
					case "C": return (int)enEpisodeType.Credits;
					case "S": return (int)enEpisodeType.Special;
					case "O": return (int)enEpisodeType.Other;
					case "T": return (int)enEpisodeType.Trailer;
					case "P": return (int)enEpisodeType.Parody;
					default: return (int)enEpisodeType.Episode;
				}
			}
		}

		public Raw_AniDB_Episode(string sRecMessage, enEpisodeSourceType sourceType)
		{
			if (sourceType == enEpisodeSourceType.Episode)
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

	public enum enEpisodeSourceType
	{
		File = 1,
		Episode = 2,
		HTTPAPI = 3
	}

	public enum enEpisodeType
	{
		Episode = 1,
		Credits = 2,
		Special = 3,
		Trailer = 4,
		Parody = 5,
		Other = 6
	}
}
