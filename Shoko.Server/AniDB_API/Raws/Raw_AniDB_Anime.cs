using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using Shoko.Commons.Utils;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Anime : XMLBase
    {
        #region Properties

        public int AnimeID { get; set; }
        public int EpisodeCount { get; set; }
        public DateTime? AirDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string URL { get; set; }
        public string Picname { get; set; }
        public int BeginYear { get; set; }
        public int EndYear { get; set; }
        public string MainTitle { get; set; }
        public string Description { get; set; }
        public int EpisodeCountNormal { get; set; }
        public int EpisodeCountSpecial { get; set; }
        public int Rating { get; set; }
        public int VoteCount { get; set; }
        public int TempRating { get; set; }
        public int TempVoteCount { get; set; }
        public int AvgReviewRating { get; set; }
        public int ReviewCount { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime? DateTimeDescUpdated { get; set; }

        [XmlIgnore]
        public int ImageEnabled { get; set; }

        public int VersionNumber { get; set; }
        public string AwardList { get; set; }
        public int Restricted { get; set; }
        public int AnimePlanetID { get; set; }
        public int ANNID { get; set; }
        public int AllCinemaID { get; set; }
        public string AnimeNfoID { get; set; }
        public string DateRecordUpdated { get; set; }

        [XmlIgnore]
        public int? LatestEpisodeNumber { get; set; }

        [XmlIgnore]
        public int AirDateAsSeconds
        {
            get { return AniDB.GetAniDBDateAsSeconds(AirDate); }
        }

        [XmlIgnore]
        public int EndDateAsSeconds
        {
            get { return AniDB.GetAniDBDateAsSeconds(EndDate); }
        }


        public string AnimeTypeRAW { get; set; }
        public string GenreRAW { get; set; }
        public string RelatedAnimeIdsRAW { get; set; }
        public string RelatedAnimeTypesRAW { get; set; }
        public string CharacterIDListRAW { get; set; }
        public string ReviewIDListRAW { get; set; }

        [XmlIgnore]
        public List<int> ReviewIDList
        {
            get
            {
                List<int> reviewIDList = new List<int>();

                try
                {
                    if (ReviewIDListRAW.Length > 0)
                    {
                        string[] reviews = ReviewIDListRAW.Split(',');
                        foreach (string rid in reviews)
                        {
                            reviewIDList.Add(int.Parse(rid));
                        }
                    }
                }
                catch
                {
                    //BaseConfig.MyAnimeLog.Write("Error trying to get reviews from anime: {0}", ex);
                }

                return reviewIDList;
            }
        }

        #endregion

        public readonly static int LastVersion = 1;

        private void PopulateDefaults()
        {
            RelatedAnimeIdsRAW = string.Empty;
            RelatedAnimeTypesRAW = string.Empty;
            CharacterIDListRAW = string.Empty;
            GenreRAW = string.Empty;
            AnimeTypeRAW = string.Empty;
            ReviewIDListRAW = string.Empty;
            AnimeID = 0;
            VersionNumber = 0;
            EpisodeCount = 0;
            AirDate = null;
            EndDate = null;
            URL = string.Empty;
            Picname = string.Empty;
            BeginYear = 0;
            EndYear = 0;
            ImageEnabled = 1;
            Description = string.Empty;
            EpisodeCountNormal = 0;
            EpisodeCountSpecial = 0;
            MainTitle = string.Empty;
            Rating = 0;
            VoteCount = 0;
            TempRating = 0;
            TempVoteCount = 0;
            AvgReviewRating = 0;
            ReviewCount = 0;
            AwardList = string.Empty;
            Restricted = 0;
            AnimePlanetID = 0;
            ANNID = 0;
            AllCinemaID = 0;
            AnimeNfoID = string.Empty;
            DateRecordUpdated = string.Empty;
            DateTimeUpdated = DateTime.Now;
            DateTimeDescUpdated = null;
        }

        // default constructor
        public Raw_AniDB_Anime()
        {
            PopulateDefaults();
        }

        // constructor
        // sRecMessage is the message received from ANIDB file info command
        // example response like '220 FILE .....'
        public Raw_AniDB_Anime(string sRecMessage)
        {
            PopulateDefaults();

            // remove the header info
            string[] sDetails = sRecMessage.Substring(10).Split('|');

            //230 ANIME 0|0
            //  0. 6107  ** anime id
            //  1. 2009  ** year
            //  2. TV Series ** type
            //  3. 979 ** related anime id's
            //  4. 32 ** related anime types
            //  5. Magic,Shounen,Manga,Military,Action ** genres
            //  6. 6,6,6,6,4 ** genreRAW weight
            //  7. Hagane no Renkinjutsushi (2009) ** romaji name
            //  8. Kanji Name
            //  9. Full Metal Alchemist: Brotherhood ** english name
            // 10.     * other name
            // 11. FMA2'hagaren2 ** short name list (the apostrophe is the list separator)
            // 12. Fullmetal Alchemist: Brotherhood'Fullmetal Alchemist 2'Full Metal Alchemist 2'???????? ???????: ????????'St?lalkemisten'????????? ??????? ?????? : ??????'2 ????????? ??????? ??????'??????? ????? 2009'Metalinis Alchemikas 2'?????? 2009 ** synonyms
            // 13. 0 ** episodes
            // 14. 5 ** normal episode count
            // 15. 6 ** special episode count
            // 16. 1238889600 ** air date
            // 17. 0 ** end date
            // 18. http://www.hagaren.jp/ ** url
            // 19. 15097.jpg ** pic name
            // 20. 175,5,273,171,4 ** category id list
            // 21. 0 ** rating
            // 22. 0 ** vote count
            // 23. 846 ** temp rating (divide by 100)
            // 24. 416 ** temp vote count
            // 25. 0 ** average review rating
            // 26. 0 ** review count
            // 27. ** Award List
            // 28. 0 ** Restricted 18+
            // 29. 0 Anime Planet ID
            // 30. 0 ANN ID
            // 31. 0 AllCinema ID
            // 32. 0 AnimeNfo ID
            // 33. 1238889600 ** last modified
            // 34. 705,2409,2410,2411,2412,2413,2414,2415 ** character id list
            // 35. 705,2409,2410,2411,2412,2413,2414,2415 ** review id list


            AnimeID = int.Parse(sDetails[0]);
            AnimeTypeRAW = AniDBAPILib.ProcessAniDBString(sDetails[2]);
            RelatedAnimeIdsRAW = AniDBAPILib.ProcessAniDBString(sDetails[3]);


            //BaseConfig.MyAnimeLog.Write("relatedAnimeIDs: {0}", sDetails[3]);
            //BaseConfig.MyAnimeLog.Write("relatedAnimeIDs: {0}", AniDBLib.ProcessAniDBString(sDetails[3]));

            RelatedAnimeTypesRAW = AniDBAPILib.ProcessAniDBString(sDetails[4]);
            //genreweight = sDetails[6];
            //RomajiName = AniDBAPILib.ProcessAniDBString(sDetails[7]);
            //BaseConfig.MyAnimeLog.Write("English name old: **{0}**", sDetails[8]);
            //KanjiName = AniDBAPILib.ProcessAniDBString(sDetails[8]);
            //EnglishName = AniDBAPILib.ProcessAniDBString(sDetails[9]);
            //BaseConfig.MyAnimeLog.Write("English name new: **{0}**", englishName);
            //OtherName = AniDBAPILib.ProcessAniDBString(sDetails[10]);
            //ShortNames = AniDBAPILib.ProcessAniDBString(sDetails[11].Replace("'", "|"));
            //Synonyms = AniDBAPILib.ProcessAniDBString(sDetails[12].Replace("'", "|"));
            EpisodeCount = AniDBAPILib.ProcessAniDBInt(sDetails[13]);
            EpisodeCountNormal = AniDBAPILib.ProcessAniDBInt(sDetails[14]);
            EpisodeCountSpecial = AniDBAPILib.ProcessAniDBInt(sDetails[15]);

            int airDateSeconds = int.Parse(AniDBAPILib.ProcessAniDBString(sDetails[16]));
            int endDateSeconds = int.Parse(AniDBAPILib.ProcessAniDBString(sDetails[17]));

            AirDate = AniDB.GetAniDBDateAsDate(airDateSeconds);
            EndDate = AniDB.GetAniDBDateAsDate(endDateSeconds);

            URL = AniDBAPILib.ProcessAniDBString(sDetails[18]);
            Picname = AniDBAPILib.ProcessAniDBString(sDetails[19]);
            //categoryidlist = sDetails[20];
            Rating = AniDBAPILib.ProcessAniDBInt(sDetails[21]);
            VoteCount = AniDBAPILib.ProcessAniDBInt(sDetails[22]);
            TempRating = AniDBAPILib.ProcessAniDBInt(sDetails[23]);
            TempVoteCount = AniDBAPILib.ProcessAniDBInt(sDetails[24]);
            AvgReviewRating = AniDBAPILib.ProcessAniDBInt(sDetails[25]);
            ReviewCount = AniDBAPILib.ProcessAniDBInt(sDetails[26]);
            AwardList = AniDBAPILib.ProcessAniDBString(sDetails[27]);
            Restricted = AniDBAPILib.ProcessAniDBInt(sDetails[28]);
            AnimePlanetID = AniDBAPILib.ProcessAniDBInt(sDetails[29]);
            ANNID = AniDBAPILib.ProcessAniDBInt(sDetails[30]);
            AllCinemaID = AniDBAPILib.ProcessAniDBInt(sDetails[31]);
            AnimeNfoID = AniDBAPILib.ProcessAniDBString(sDetails[32]);
            DateRecordUpdated = AniDBAPILib.ProcessAniDBString(sDetails[33]);
            CharacterIDListRAW = AniDBAPILib.ProcessAniDBString(sDetails[34]);
            ReviewIDListRAW = AniDBAPILib.ProcessAniDBString(sDetails[35]);
            //New version number
            VersionNumber = LastVersion;
            //Genres should by right in utf-16
            GenreRAW = sDetails[5];
            BeginYear = AirDate.HasValue ? AirDate.Value.Year : 0;
            EndYear = EndDate.HasValue ? EndDate.Value.Year : 0;
        }


        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("AnimeID: " + AnimeID);
            sb.Append(" | Main Title: " + MainTitle);
            sb.Append(" | EpisodeCount: " + EpisodeCount);
            sb.Append(" | AirDate: " + AirDate);
            sb.Append(" | Picname: " + Picname);
            sb.Append(" | Type: " + AnimeTypeRAW);
            sb.Append(" | relatedAnimeIDs: " + RelatedAnimeIdsRAW);
            sb.Append(" | relatedAnimeTypes: " + RelatedAnimeTypesRAW);
            return sb.ToString();
        }
    }
}