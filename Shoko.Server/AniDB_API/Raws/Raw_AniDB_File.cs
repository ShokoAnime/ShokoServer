using System;
using System.Text;
using System.Xml.Serialization;
using Shoko.Server;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_File : XMLBase, IHash
    {
        #region Properties

        public int FileID { get; set; }

        public string ED2KHash { get; set; }

        public int AnimeID { get; set; }

        public int GroupID { get; set; }

        public string File_Source { get; set; }

        public string File_AudioCodec { get; set; }

        public string File_VideoCodec { get; set; }

        public string File_VideoResolution { get; set; }

        public string File_FileExtension { get; set; }

        public int File_LengthSeconds { get; set; }

        public string File_Description { get; set; }

        public int File_ReleaseDate { get; set; }

        public string Anime_GroupName { get; set; }

        public string Anime_GroupNameShort { get; set; }

        public int Episode_Rating { get; set; }

        public int Episode_Votes { get; set; }

        public DateTime DateTimeUpdated { get; set; }

        [XmlIgnore]
        public int IsWatched { get; set; }

        public string CRC { get; set; }

        public string MD5 { get; set; }

        public string SHA1 { get; set; }

        public long FileSize { get; set; }

        [XmlIgnore]
        public int Version { get; set; }

        public string FileName { get; set; }

        public string SubtitlesRAW { get; set; }

        public string LanguagesRAW { get; set; }

        public string EpisodesRAW { get; set; }

        public string EpisodesPercentRAW { get; set; }

        public int FileVersion { get; set; }

        public int IsCensored { get; set; }

        public int IsDeprecated { get; set; }

	    public int IsChaptered { get; set; }

        public int InternalVersion
        {
            get { return 2; }
        }

        public string OtherEpisodesRAW { get; set; }

        #endregion

        // default constructor
        public Raw_AniDB_File()
        {
            InitDefaultValues();
        }

        private void InitDefaultValues()
        {
            FileID = 0;
            ED2KHash = string.Empty;
            MD5 = string.Empty;
            SHA1 = string.Empty;
            FileSize = 0;
            AnimeID = 0;
            GroupID = 0;
            CRC = string.Empty;
            File_LengthSeconds = 0;
            File_Source = string.Empty;
            File_AudioCodec = string.Empty;
            File_VideoCodec = string.Empty;
            File_VideoResolution = string.Empty;
            File_FileExtension = string.Empty;
            File_Description = string.Empty;
            File_ReleaseDate = 0;
            Anime_GroupName = string.Empty;
            Anime_GroupNameShort = string.Empty;
            FileName = string.Empty;
            Episode_Rating = 0;
            Episode_Votes = 0;
            DateTimeUpdated = DateTime.Now;
            Version = 0;
            IsWatched = 0;
            FileVersion = 1;
            IsCensored = 0;
            IsDeprecated = 0;
            OtherEpisodesRAW = string.Empty;
        }

        public string Info
        {
            get
            {
                if (string.IsNullOrEmpty(FileName))
                    return FileID.ToString();
                return FileName;
            }
        }

        public readonly static int LastVersion = 2;

        public Raw_AniDB_File(string sRecMessage)
        {
            InitDefaultValues();

            // remove the header info
            string[] sDetails = sRecMessage.Substring(9).Split('|');

            //BaseConfig.MyAnimeLog.Write("PROCESSING FILE: {0}", sDetails.Length);

            // 220 FILE
            // 0. 572794 ** fileid
            // 1. 6107 ** anime id
            // 2. 99294 ** episode id
            // 3. 12 ** group id
            // 4. 2723 ** lid
            // 5. ** other episodes
            // 6. ** isDeprecated
            // 7. state

            // 8 ** Size
            // 9. c646d82a184a33f4e4f98af39f29a044 ** ed2k hash
            // 10. ** md5
            // 11. ** sha1
            // 12. 8452c4bf ** crc32
            // 13. high ** quality
            // 14. HDTV ** source
            // 15. Vorbis (Ogg Vorbis) ** audio codec
            // 16. 148 ** audio bit rate
            // 17. H264/AVC ** video codec
            // 18. 1773 ** video bit rate
            // 19. 1280x720 ** video res
            // 20. mkv ** file extension

            // 21. Audio Langugages.
            // 22. Subtitle languages.


            // 23. 1470 ** length in seconds
            // 24.   ** description
            // 25. 1239494400 ** release date ** date is the time of the event (in seconds since 1.1.1970) 

            // 26. Episode FileName

            // 27. 2 ** episode #
            // 28. The Day It Began ** ep name 
            // 29. Hajimari no Hi ** ep name romaji
            // 30. ** ep name kanji
            // 31. 712 ** episode rating (7.12)
            // 32. 14 ** episode vote count
            // 33. Eclipse Productions ** group name
            // 34. Eclipse ** group name short

            this.FileSize = long.Parse(sDetails[8].Trim());
            this.ED2KHash = AniDBAPILib.ProcessAniDBString(sDetails[9].Trim()).ToUpper();
            this.MD5 = AniDBAPILib.ProcessAniDBString(sDetails[10].Trim()).ToUpper();
            this.SHA1 = AniDBAPILib.ProcessAniDBString(sDetails[11].Trim()).ToUpper();
            this.CRC = AniDBAPILib.ProcessAniDBString(sDetails[12].Trim()).ToUpper();
            FileID = int.Parse(sDetails[0].Trim());
            AnimeID = int.Parse(sDetails[1].Trim());

            int state = int.Parse(sDetails[7].Trim());
            if (state == 0 || state == 1)
                FileVersion = 1;
            else
            {
                AniDBFileFlags eFlags = (AniDBFileFlags) state;
                if (BitMaskHelper.IsSet(eFlags, AniDBFileFlags.FILE_ISV2)) FileVersion = 2;
                if (BitMaskHelper.IsSet(eFlags, AniDBFileFlags.FILE_ISV3)) FileVersion = 3;
                if (BitMaskHelper.IsSet(eFlags, AniDBFileFlags.FILE_ISV4)) FileVersion = 4;
                if (BitMaskHelper.IsSet(eFlags, AniDBFileFlags.FILE_ISV5)) FileVersion = 5;

                if (BitMaskHelper.IsSet(eFlags, AniDBFileFlags.FILE_CEN))
                    IsCensored = 1;

	            IsChaptered = BitMaskHelper.IsSet(eFlags, AniDBFileFlags.FILE_CHAPTERED) ? 1 : 0;
            }

            if (int.TryParse(sDetails[6].Trim(), out int isdep))
                IsDeprecated = isdep == 0 ? 0 : 1;

            EpisodesRAW = sDetails[2].Trim();
            EpisodesPercentRAW = "100";
            OtherEpisodesRAW = string.Empty;
            if (sDetails[5].Trim().Length > 0)
            {
                string[] Eps = sDetails[5].Trim().Split('\'');
                if (Eps.Length > 0)
                {
                    foreach (string ep in Eps)
                    {
                        string[] ep2 = ep.Split(',');
                        if (ep2.Length > 0)
                        {
                            EpisodesRAW += "," + ep2[0];
                            if (!string.IsNullOrEmpty(OtherEpisodesRAW))
                                OtherEpisodesRAW += ",";
                            OtherEpisodesRAW += ep2[0];
                        }
                        if (ep2.Length > 1)
                            EpisodesPercentRAW += "," + ep2[1];
                        else
                            EpisodesPercentRAW += ",100";
                    }
                }
            }
            // Other Episodes have this format : FileId, % From The End, Per example 1030, 100 Means the complete 1030 Episode
            // 1030, 49 Means content starts at 51% of the episode 1030. 

            GroupID = int.Parse(sDetails[3].Trim());
            File_Source = AniDBAPILib.ProcessAniDBString(sDetails[14].Trim());
            File_AudioCodec = AniDBAPILib.ProcessAniDBString(sDetails[15].Trim());
            File_VideoCodec = AniDBAPILib.ProcessAniDBString(sDetails[17].Trim());
            File_VideoResolution = AniDBAPILib.ProcessAniDBString(sDetails[19].Trim());
            File_FileExtension = AniDBAPILib.ProcessAniDBString(sDetails[20].Trim());

            File_LengthSeconds = AniDBAPILib.ProcessAniDBInt(sDetails[23].Trim());
            File_Description = AniDBAPILib.ProcessAniDBString(sDetails[24].Trim());
            File_ReleaseDate = AniDBAPILib.ProcessAniDBInt(sDetails[25].Trim());


            LanguagesRAW = AniDBAPILib.ProcessAniDBString(sDetails[21].Trim());
            SubtitlesRAW = AniDBAPILib.ProcessAniDBString(sDetails[22].Trim());

            FileName = AniDBAPILib.ProcessAniDBString(sDetails[26].Trim());

            // mylist values
            string mlState = sDetails[27].Trim();
            string mlFileState = sDetails[28].Trim();
            string mlViewed = sDetails[29].Trim();
            string mlViewDate = sDetails[30].Trim();
            string mlStorage = sDetails[31].Trim();
            string mlSource = sDetails[32].Trim();
            string mlOther = sDetails[33].Trim();

            // amask
            Anime_GroupName = AniDBAPILib.ProcessAniDBString(sDetails[40].Trim());
            Anime_GroupNameShort = AniDBAPILib.ProcessAniDBString(sDetails[41].Trim());

            IsWatched = 0; // 0 = false, 1 = true
            Episode_Rating = AniDBAPILib.ProcessAniDBInt(sDetails[38].Trim());
            Episode_Votes = AniDBAPILib.ProcessAniDBInt(sDetails[39].Trim());

            Version = LastVersion;
        }

        /*public Raw_AniDB_File(string sRecMessage)
		{
			InitDefaultValues();

			// remove the header info
			string[] sDetails = sRecMessage.Substring(9).Split('|');

			//BaseConfig.MyAnimeLog.Write("PROCESSING FILE: {0}", sDetails.Length);

			// 220 FILE
			// 0. 572794 ** fileid
			// 1. 6107 ** anime id
			// 2. 99294 ** episode id
			// 3. 12 ** group id
			// 4. 2723 ** lid

			// 5. ** other episodes

			// 6 ** Size
			// 7. c646d82a184a33f4e4f98af39f29a044 ** ed2k hash
			// 8. ** md5
			// 9. ** sha1
			// 10. 8452c4bf ** crc32
			// 11. high ** quality
			// 12. HDTV ** source
			// 13. Vorbis (Ogg Vorbis) ** audio codec
			// 14. 148 ** audio bit rate
			// 15. H264/AVC ** video codec
			// 16. 1773 ** video bit rate
			// 17. 1280x720 ** video res
			// 18. mkv ** file extension

			// 19. Audio Langugages.
			// 20. Subtitle languages.


			// 21. 1470 ** length in seconds
			// 22.   ** description
			// 23. 1239494400 ** release date ** date is the time of the event (in seconds since 1.1.1970) 

			// 24. Episode FileName

			// 25. 2 ** episode #
			// 26. The Day It Began ** ep name 
			// 27. Hajimari no Hi ** ep name romaji
			// 28. ** ep name kanji
			// 29. 712 ** episode rating (7.12)
			// 30. 14 ** episode vote count
			// 31. Eclipse Productions ** group name
			// 32. Eclipse ** group name short

			this.FileSize = long.Parse(sDetails[6].Trim());
			this.ED2KHash = AniDBAPILib.ProcessAniDBString(sDetails[7].Trim()).ToUpper();
			this.MD5 = AniDBAPILib.ProcessAniDBString(sDetails[8].Trim()).ToUpper();
			this.SHA1 = AniDBAPILib.ProcessAniDBString(sDetails[9].Trim()).ToUpper();
			this.CRC = AniDBAPILib.ProcessAniDBString(sDetails[10].Trim()).ToUpper();
			FileID = int.Parse(sDetails[0].Trim());
			AnimeID = int.Parse(sDetails[1].Trim());

			EpisodesRAW = sDetails[2].Trim();
			EpisodesPercentRAW = "100";
			if (sDetails[5].Trim().Length > 0)
			{
				string[] Eps = sDetails[5].Trim().Split('\'');
				if (Eps.Length > 0)
				{
					foreach (string ep in Eps)
					{
						string[] ep2 = ep.Split(',');
						if (ep2.Length > 0)
							EpisodesRAW += "," + ep2[0];
						if (ep2.Length > 1)
							EpisodesPercentRAW += "," + ep2[1];
						else
							EpisodesPercentRAW += ",100";
					}
				}
			}
			// Other Episodes have this format : FileId, % From The End, Per example 1030, 100 Means the complete 1030 Episode
			// 1030, 49 Means content starts at 51% of the episode 1030. 

			GroupID = int.Parse(sDetails[3].Trim());
			File_Source = AniDBAPILib.ProcessAniDBString(sDetails[12].Trim());
			File_AudioCodec = AniDBAPILib.ProcessAniDBString(sDetails[13].Trim());
			File_VideoCodec = AniDBAPILib.ProcessAniDBString(sDetails[15].Trim());
			File_VideoResolution = AniDBAPILib.ProcessAniDBString(sDetails[17].Trim());
			File_FileExtension = AniDBAPILib.ProcessAniDBString(sDetails[18].Trim());

			File_LengthSeconds = AniDBAPILib.ProcessAniDBInt(sDetails[21].Trim());
			File_Description = AniDBAPILib.ProcessAniDBString(sDetails[22].Trim());
			File_ReleaseDate = AniDBAPILib.ProcessAniDBInt(sDetails[23].Trim());
			Anime_GroupName = AniDBAPILib.ProcessAniDBString(sDetails[31].Trim());
			Anime_GroupNameShort = AniDBAPILib.ProcessAniDBString(sDetails[32].Trim());

			IsWatched = 0; // 0 = false, 1 = true
			Episode_Rating = AniDBAPILib.ProcessAniDBInt(sDetails[29].Trim());
			Episode_Votes = AniDBAPILib.ProcessAniDBInt(sDetails[30].Trim());

			LanguagesRAW = AniDBAPILib.ProcessAniDBString(sDetails[19].Trim());
			SubtitlesRAW = AniDBAPILib.ProcessAniDBString(sDetails[20].Trim());

			FileName = AniDBAPILib.ProcessAniDBString(sDetails[24].Trim());

			Version = LastVersion;
		}*/

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Raw_AniDB_File:: hash: " + ED2KHash);
            sb.Append(" | fileID: " + FileID.ToString());
            sb.Append(" | Source: " + File_Source);
            //sb.Append(" | episodeID: " + EpisodeID.ToString());
            sb.Append(" | animeID: " + AnimeID.ToString());
            sb.Append(" | IsWatched: " + IsWatched.ToString());
            sb.Append(" | FileName: " + FileName);
            sb.Append(" | FileSize: " + FileSize.ToString());
            sb.Append(" | DateTimeUpdated: " + DateTimeUpdated.ToString());
            sb.Append(" | EpisodesRAW: " + EpisodesRAW.ToString());
            sb.Append(" | EpisodesPercentRAW: " + EpisodesPercentRAW);
            sb.Append(" | LanguagesRAW: " + LanguagesRAW);
            sb.Append(" | SubtitlesRAW: " + SubtitlesRAW);
            return sb.ToString();
        }
    }
}