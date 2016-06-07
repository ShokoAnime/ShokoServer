using System;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.Entities;

namespace JMMServer.WebCache
{
    [Serializable]
    [XmlRoot("AniDB_FileRequest")]
    public class AniDB_FileRequest : XMLBase
    {
        protected string anime_GroupName = "";

        protected string anime_GroupNameShort = "";

        protected int animeID;

        protected string cRC = "";

        protected int episode_Rating;

        protected int episode_Votes;

        protected string episodesPercentRAW = "";

        protected string episodesRAW = "";

        protected string file_AudioCodec = "";

        protected string file_Description = "";

        protected string file_FileExtension = "";

        protected int file_LengthSeconds;

        protected int file_ReleaseDate;

        protected string file_Source = "";

        protected string file_VideoCodec = "";

        protected string file_VideoResolution = "";
        protected int fileID;

        protected string fileName = "";

        protected long fileSize;

        protected int groupID;

        protected string hash = "";

        protected string languagesRAW = "";

        protected string mD5 = "";

        protected string sHA1 = "";

        protected string subtitlesRAW = "";

        // default constructor
        public AniDB_FileRequest()
        {
        }

        // default constructor
        public AniDB_FileRequest(AniDB_File data)
        {
            Anime_GroupName = data.Anime_GroupName;
            Anime_GroupNameShort = data.Anime_GroupNameShort;
            AnimeID = data.AnimeID;
            CRC = data.CRC;
            Episode_Rating = data.Episode_Rating;
            Episode_Votes = data.Episode_Votes;
            File_AudioCodec = data.File_AudioCodec;
            File_Description = data.File_Description;
            File_FileExtension = data.File_FileExtension;
            File_LengthSeconds = data.File_LengthSeconds;
            File_ReleaseDate = data.File_ReleaseDate;
            File_Source = data.File_Source;
            File_VideoCodec = data.File_VideoCodec;
            File_VideoResolution = data.File_VideoResolution;
            FileID = data.FileID;
            FileName = data.FileName;
            FileSize = data.FileSize;
            GroupID = data.GroupID;
            Hash = data.Hash;
            MD5 = data.MD5;
            SHA1 = data.SHA1;

            SubtitlesRAW = data.SubtitlesRAWForWebCache;
            LanguagesRAW = data.LanguagesRAWForWebCache;
            EpisodesRAW = data.EpisodesRAWForWebCache;
            EpisodesPercentRAW = data.EpisodesPercentRAWForWebCache;
        }

        public int FileID
        {
            get { return fileID; }
            set { fileID = value; }
        }

        public string Hash
        {
            get { return hash; }
            set { hash = value; }
        }

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        public int GroupID
        {
            get { return groupID; }
            set { groupID = value; }
        }

        public string File_Source
        {
            get { return file_Source; }
            set { file_Source = value; }
        }

        public string File_AudioCodec
        {
            get { return file_AudioCodec; }
            set { file_AudioCodec = value; }
        }

        public string File_VideoCodec
        {
            get { return file_VideoCodec; }
            set { file_VideoCodec = value; }
        }

        public string File_VideoResolution
        {
            get { return file_VideoResolution; }
            set { file_VideoResolution = value; }
        }

        public string File_FileExtension
        {
            get { return file_FileExtension; }
            set { file_FileExtension = value; }
        }

        public int File_LengthSeconds
        {
            get { return file_LengthSeconds; }
            set { file_LengthSeconds = value; }
        }

        public string File_Description
        {
            get { return file_Description; }
            set { file_Description = value; }
        }

        public int File_ReleaseDate
        {
            get { return file_ReleaseDate; }
            set { file_ReleaseDate = value; }
        }

        public string Anime_GroupName
        {
            get { return anime_GroupName; }
            set { anime_GroupName = value; }
        }

        public string Anime_GroupNameShort
        {
            get { return anime_GroupNameShort; }
            set { anime_GroupNameShort = value; }
        }

        public int Episode_Rating
        {
            get { return episode_Rating; }
            set { episode_Rating = value; }
        }

        public int Episode_Votes
        {
            get { return episode_Votes; }
            set { episode_Votes = value; }
        }

        public string CRC
        {
            get { return cRC; }
            set { cRC = value; }
        }

        public string MD5
        {
            get { return mD5; }
            set { mD5 = value; }
        }

        public string SHA1
        {
            get { return sHA1; }
            set { sHA1 = value; }
        }

        public string FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }

        public long FileSize
        {
            get { return fileSize; }
            set { fileSize = value; }
        }

        public string SubtitlesRAW
        {
            get { return subtitlesRAW; }
            set { subtitlesRAW = value; }
        }

        public string LanguagesRAW
        {
            get { return languagesRAW; }
            set { languagesRAW = value; }
        }

        public string EpisodesRAW
        {
            get { return episodesRAW; }
            set { episodesRAW = value; }
        }

        public string EpisodesPercentRAW
        {
            get { return episodesPercentRAW; }
            set { episodesPercentRAW = value; }
        }
    }
}