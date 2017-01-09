using System;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.WebCache
{
    [Serializable]
    [XmlRoot("AniDB_FileRequest")]
    public class AniDB_FileRequest : XMLBase
    {
        protected int fileID = 0;

        public int FileID
        {
            get { return fileID; }
            set { fileID = value; }
        }

        protected string hash = "";

        public string Hash
        {
            get { return hash; }
            set { hash = value; }
        }

        protected int animeID = 0;

        public int AnimeID
        {
            get { return animeID; }
            set { animeID = value; }
        }

        protected int groupID = 0;

        public int GroupID
        {
            get { return groupID; }
            set { groupID = value; }
        }

        protected string file_Source = "";

        public string File_Source
        {
            get { return file_Source; }
            set { file_Source = value; }
        }

        protected string file_AudioCodec = "";

        public string File_AudioCodec
        {
            get { return file_AudioCodec; }
            set { file_AudioCodec = value; }
        }

        protected string file_VideoCodec = "";

        public string File_VideoCodec
        {
            get { return file_VideoCodec; }
            set { file_VideoCodec = value; }
        }

        protected string file_VideoResolution = "";

        public string File_VideoResolution
        {
            get { return file_VideoResolution; }
            set { file_VideoResolution = value; }
        }

        protected string file_FileExtension = "";

        public string File_FileExtension
        {
            get { return file_FileExtension; }
            set { file_FileExtension = value; }
        }

        protected int file_LengthSeconds = 0;

        public int File_LengthSeconds
        {
            get { return file_LengthSeconds; }
            set { file_LengthSeconds = value; }
        }

        protected string file_Description = "";

        public string File_Description
        {
            get { return file_Description; }
            set { file_Description = value; }
        }

        protected int file_ReleaseDate = 0;

        public int File_ReleaseDate
        {
            get { return file_ReleaseDate; }
            set { file_ReleaseDate = value; }
        }

        protected string anime_GroupName = "";

        public string Anime_GroupName
        {
            get { return anime_GroupName; }
            set { anime_GroupName = value; }
        }

        protected string anime_GroupNameShort = "";

        public string Anime_GroupNameShort
        {
            get { return anime_GroupNameShort; }
            set { anime_GroupNameShort = value; }
        }

        protected int episode_Rating = 0;

        public int Episode_Rating
        {
            get { return episode_Rating; }
            set { episode_Rating = value; }
        }

        protected int episode_Votes = 0;

        public int Episode_Votes
        {
            get { return episode_Votes; }
            set { episode_Votes = value; }
        }

        protected string cRC = "";

        public string CRC
        {
            get { return cRC; }
            set { cRC = value; }
        }

        protected string mD5 = "";

        public string MD5
        {
            get { return mD5; }
            set { mD5 = value; }
        }

        protected string sHA1 = "";

        public string SHA1
        {
            get { return sHA1; }
            set { sHA1 = value; }
        }

        protected string fileName = "";

        public string FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }

        protected long fileSize = 0;

        public long FileSize
        {
            get { return fileSize; }
            set { fileSize = value; }
        }

        protected string subtitlesRAW = "";

        public string SubtitlesRAW
        {
            get { return subtitlesRAW; }
            set { subtitlesRAW = value; }
        }

        protected string languagesRAW = "";

        public string LanguagesRAW
        {
            get { return languagesRAW; }
            set { languagesRAW = value; }
        }

        protected string episodesRAW = "";

        public string EpisodesRAW
        {
            get { return episodesRAW; }
            set { episodesRAW = value; }
        }

        protected string episodesPercentRAW = "";

        public string EpisodesPercentRAW
        {
            get { return episodesPercentRAW; }
            set { episodesPercentRAW = value; }
        }

        // default constructor
        public AniDB_FileRequest()
        {
        }

        // default constructor
        public AniDB_FileRequest(SVR_AniDB_File data)
        {
            this.Anime_GroupName = data.Anime_GroupName;
            this.Anime_GroupNameShort = data.Anime_GroupNameShort;
            //this.AnimeID = data.AnimeID;
            this.CRC = data.CRC;
            this.Episode_Rating = data.Episode_Rating;
            this.Episode_Votes = data.Episode_Votes;
            this.File_AudioCodec = data.File_AudioCodec;
            this.File_Description = data.File_Description;
            this.File_FileExtension = data.File_FileExtension;
            this.File_LengthSeconds = data.File_LengthSeconds;
            this.File_ReleaseDate = data.File_ReleaseDate;
            this.File_Source = data.File_Source;
            this.File_VideoCodec = data.File_VideoCodec;
            this.File_VideoResolution = data.File_VideoResolution;
            this.FileID = data.FileID;
            this.FileName = data.FileName;
            this.FileSize = data.FileSize;
            this.GroupID = data.GroupID;
            this.Hash = data.Hash;
            this.MD5 = data.MD5;
            this.SHA1 = data.SHA1;

            this.SubtitlesRAW = data.SubtitlesRAWForWebCache;
            this.LanguagesRAW = data.LanguagesRAWForWebCache;
            this.EpisodesRAW = data.EpisodesRAWForWebCache;
            this.EpisodesPercentRAW = data.EpisodesPercentRAWForWebCache;
        }
    }
}