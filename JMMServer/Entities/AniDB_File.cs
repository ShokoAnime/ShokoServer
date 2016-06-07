using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.Repositories;
using JMMServer.WebCache;
using NLog;

namespace JMMServer.Entities
{
    public class AniDB_File
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private string episodesPercentRAW;
        private string episodesRAW;
        private string languagesRAW;


        private string subtitlesRAW;


        [XmlIgnore]
        public List<Language> Languages
        {
            get
            {
                var lans = new List<Language>();

                var repFileLanguages = new CrossRef_Languages_AniDB_FileRepository();
                var repLanguages = new LanguageRepository();

                var fileLanguages = repFileLanguages.GetByFileID(FileID);

                foreach (var crossref in fileLanguages)
                {
                    var lan = repLanguages.GetByID(crossref.LanguageID);
                    if (lan != null) lans.Add(lan);
                }
                return lans;
            }
        }


        [XmlIgnore]
        public List<Language> Subtitles
        {
            get
            {
                var subs = new List<Language>();

                var repFileSubtitles = new CrossRef_Subtitles_AniDB_FileRepository();
                var repLanguages = new LanguageRepository();

                var fileSubtitles = repFileSubtitles.GetByFileID(FileID);


                foreach (var crossref in fileSubtitles)
                {
                    var sub = repLanguages.GetByID(crossref.LanguageID);
                    if (sub != null) subs.Add(sub);
                }
                return subs;
            }
        }

        [XmlIgnore]
        public List<int> EpisodeIDs
        {
            get
            {
                var ids = new List<int>();

                var repFileEps = new CrossRef_File_EpisodeRepository();
                var fileEps = repFileEps.GetByHash(Hash);

                foreach (var crossref in fileEps)
                {
                    ids.Add(crossref.EpisodeID);
                }
                return ids;
            }
        }

        [XmlIgnore]
        public List<AniDB_Episode> Episodes
        {
            get
            {
                var eps = new List<AniDB_Episode>();

                var repFileEps = new CrossRef_File_EpisodeRepository();
                var fileEps = repFileEps.GetByHash(Hash);

                foreach (var crossref in fileEps)
                {
                    if (crossref.Episode != null) eps.Add(crossref.Episode);
                }
                return eps;
            }
        }

        [XmlIgnore]
        public List<CrossRef_File_Episode> EpisodeCrossRefs
        {
            get
            {
                var repFileEps = new CrossRef_File_EpisodeRepository();
                return repFileEps.GetByHash(Hash);
            }
        }


        public string SubtitlesRAW
        {
            get
            {
                if (!string.IsNullOrEmpty(subtitlesRAW))
                    return subtitlesRAW;
                var ret = "";
                foreach (var lang in Subtitles)
                {
                    if (ret.Length > 0)
                        ret += ",";
                    ret += lang.LanguageName;
                }
                return ret;
            }
            set { subtitlesRAW = value; }
        }


        public string LanguagesRAW
        {
            get
            {
                if (!string.IsNullOrEmpty(languagesRAW))
                    return languagesRAW;
                var ret = "";
                foreach (var lang in Languages)
                {
                    if (ret.Length > 0)
                        ret += ",";
                    ret += lang.LanguageName;
                }
                return ret;
            }
            set { languagesRAW = value; }
        }


        public string EpisodesRAW
        {
            get
            {
                if (!string.IsNullOrEmpty(episodesRAW))
                    return episodesRAW;
                var ret = "";
                foreach (var cross in EpisodeCrossRefs)
                {
                    if (ret.Length > 0)
                        ret += ", ";
                    ret += cross.EpisodeID.ToString();
                }
                return ret;
            }
            set { episodesRAW = value; }
        }


        public string EpisodesPercentRAW
        {
            get
            {
                if (!string.IsNullOrEmpty(episodesPercentRAW))
                    return episodesPercentRAW;
                var ret = "";
                foreach (var cross in EpisodeCrossRefs)
                {
                    if (ret.Length > 0)
                        ret += ", ";
                    ret += cross.Percentage.ToString();
                }
                return ret;
            }
            set { episodesPercentRAW = value; }
        }

        public string SubtitlesRAWForWebCache
        {
            get
            {
                var apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(subtitlesRAW))
                    return subtitlesRAW;
                var ret = "";
                foreach (var lang in Subtitles)
                {
                    if (ret.Length > 0)
                        ret += apostrophe;
                    ret += lang.LanguageName;
                }
                return ret;
            }
            set { subtitlesRAW = value; }
        }


        public string LanguagesRAWForWebCache
        {
            get
            {
                var apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(languagesRAW))
                    return languagesRAW;
                var ret = "";
                foreach (var lang in Languages)
                {
                    if (ret.Length > 0)
                        ret += apostrophe;
                    ret += lang.LanguageName;
                }
                return ret;
            }
            set { languagesRAW = value; }
        }


        public string EpisodesRAWForWebCache
        {
            get
            {
                var apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(episodesRAW))
                    return episodesRAW;
                var ret = "";
                foreach (var cross in EpisodeCrossRefs)
                {
                    if (ret.Length > 0)
                        ret += apostrophe;
                    ret += cross.EpisodeID.ToString();
                }
                return ret;
            }
            set { episodesRAW = value; }
        }


        public string EpisodesPercentRAWForWebCache
        {
            get
            {
                var apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(episodesPercentRAW))
                    return episodesPercentRAW;
                var ret = "";
                foreach (var cross in EpisodeCrossRefs)
                {
                    if (ret.Length > 0)
                        ret += apostrophe;
                    ret += cross.Percentage.ToString();
                }
                return ret;
            }
            set { episodesPercentRAW = value; }
        }


        public void Populate(Raw_AniDB_File fileInfo)
        {
            Anime_GroupName = fileInfo.Anime_GroupName;
            Anime_GroupNameShort = fileInfo.Anime_GroupNameShort;
            AnimeID = fileInfo.AnimeID;
            CRC = fileInfo.CRC;
            DateTimeUpdated = DateTime.Now;
            Episode_Rating = fileInfo.Episode_Rating;
            Episode_Votes = fileInfo.Episode_Votes;
            File_AudioCodec = fileInfo.File_AudioCodec;
            File_Description = fileInfo.File_Description;
            File_FileExtension = fileInfo.File_FileExtension;
            File_LengthSeconds = fileInfo.File_LengthSeconds;
            File_ReleaseDate = fileInfo.File_ReleaseDate;
            File_Source = fileInfo.File_Source;
            File_VideoCodec = fileInfo.File_VideoCodec;
            File_VideoResolution = fileInfo.File_VideoResolution;
            FileID = fileInfo.FileID;
            FileName = fileInfo.FileName;
            FileSize = fileInfo.FileSize;
            GroupID = fileInfo.GroupID;
            Hash = fileInfo.ED2KHash;
            IsWatched = fileInfo.IsWatched;
            MD5 = fileInfo.MD5;
            SHA1 = fileInfo.SHA1;
            FileVersion = fileInfo.FileVersion;
            IsCensored = fileInfo.IsCensored;
            IsDeprecated = fileInfo.IsDeprecated;
            InternalVersion = fileInfo.InternalVersion;

            languagesRAW = fileInfo.LanguagesRAW;
            subtitlesRAW = fileInfo.SubtitlesRAW;
            episodesPercentRAW = fileInfo.EpisodesPercentRAW;
            episodesRAW = fileInfo.EpisodesRAW;
        }

        public void Populate(AniDB_FileRequest fileInfo)
        {
            Anime_GroupName = fileInfo.Anime_GroupName;
            Anime_GroupNameShort = fileInfo.Anime_GroupNameShort;
            AnimeID = fileInfo.AnimeID;
            CRC = fileInfo.CRC;
            DateTimeUpdated = DateTime.Now;
            Episode_Rating = fileInfo.Episode_Rating;
            Episode_Votes = fileInfo.Episode_Votes;
            File_AudioCodec = fileInfo.File_AudioCodec;
            File_Description = fileInfo.File_Description;
            File_FileExtension = fileInfo.File_FileExtension;
            File_LengthSeconds = fileInfo.File_LengthSeconds;
            File_ReleaseDate = fileInfo.File_ReleaseDate;
            File_Source = fileInfo.File_Source;
            File_VideoCodec = fileInfo.File_VideoCodec;
            File_VideoResolution = fileInfo.File_VideoResolution;
            FileID = fileInfo.FileID;
            FileName = fileInfo.FileName;
            FileSize = fileInfo.FileSize;
            GroupID = fileInfo.GroupID;
            Hash = fileInfo.Hash;
            MD5 = fileInfo.MD5;
            SHA1 = fileInfo.SHA1;

            FileVersion = 1;
            IsCensored = 0;
            IsDeprecated = 0;
            InternalVersion = 0;

            languagesRAW = fileInfo.LanguagesRAW;
            subtitlesRAW = fileInfo.SubtitlesRAW;
            episodesPercentRAW = fileInfo.EpisodesPercentRAW;
            episodesRAW = fileInfo.EpisodesRAW;
        }

        public void CreateLanguages()
        {
            var apostrophe = "'".ToCharArray()[0];

            var repLanguages = new LanguageRepository();

            if (languagesRAW != null) //Only create relations if the origin of the data if from Raw (WebService/AniDB)
            {
                if (languagesRAW.Trim().Length == 0) return;
                // Delete old if changed

                var repFileLanguages = new CrossRef_Languages_AniDB_FileRepository();


                var fileLanguages = repFileLanguages.GetByFileID(FileID);
                foreach (var fLan in fileLanguages)
                {
                    repFileLanguages.Delete(fLan.CrossRef_Languages_AniDB_FileID);
                }


                var langs = languagesRAW.Split(apostrophe);
                foreach (var language in langs)
                {
                    var rlan = language.Trim().ToLower();
                    if (rlan.Length > 0)
                    {
                        var lan = repLanguages.GetByLanguageName(rlan);
                        if (lan == null)
                        {
                            lan = new Language();
                            lan.LanguageName = rlan;
                            repLanguages.Save(lan);
                        }
                        var cross = new CrossRef_Languages_AniDB_File();
                        cross.LanguageID = lan.LanguageID;
                        cross.FileID = FileID;
                        repFileLanguages.Save(cross);
                    }
                }
            }

            if (subtitlesRAW != null)
            {
                if (subtitlesRAW.Trim().Length == 0) return;

                // Delete old if changed
                var repFileSubtitles = new CrossRef_Subtitles_AniDB_FileRepository();
                var fileSubtitles = repFileSubtitles.GetByFileID(FileID);

                foreach (var fSub in fileSubtitles)
                {
                    repFileSubtitles.Delete(fSub.CrossRef_Subtitles_AniDB_FileID);
                }

                var subs = subtitlesRAW.Split(apostrophe);
                foreach (var language in subs)
                {
                    var rlan = language.Trim().ToLower();
                    if (rlan.Length > 0)
                    {
                        var lan = repLanguages.GetByLanguageName(rlan);
                        if (lan == null)
                        {
                            lan = new Language();
                            lan.LanguageName = rlan;
                            repLanguages.Save(lan);
                        }
                        var cross = new CrossRef_Subtitles_AniDB_File();
                        cross.LanguageID = lan.LanguageID;
                        cross.FileID = FileID;
                        repFileSubtitles.Save(cross);
                    }
                }
            }
        }

        public void CreateCrossEpisodes(string localFileName)
        {
            if (episodesRAW != null) //Only create relations if the origin of the data if from Raw (AniDB)
            {
                var repFileEpisodes = new CrossRef_File_EpisodeRepository();
                var fileEps = repFileEpisodes.GetByHash(Hash);

                foreach (var fileEp in fileEps)
                    repFileEpisodes.Delete(fileEp.CrossRef_File_EpisodeID);

                var apostrophe = "'".ToCharArray()[0];
                var epiSplit = ',';
                if (episodesRAW.Contains(apostrophe))
                    epiSplit = apostrophe;

                var eppSplit = ',';
                if (episodesPercentRAW.Contains(apostrophe))
                    eppSplit = apostrophe;

                var epi = episodesRAW.Split(epiSplit);
                var epp = episodesPercentRAW.Split(eppSplit);
                for (var x = 0; x < epi.Length; x++)
                {
                    var epis = epi[x].Trim();
                    var epps = epp[x].Trim();
                    if (epis.Length > 0)
                    {
                        var epid = 0;
                        int.TryParse(epis, out epid);
                        var eppp = 100;
                        int.TryParse(epps, out eppp);
                        if (epid != 0)
                        {
                            var cross = new CrossRef_File_Episode();
                            cross.Hash = Hash;
                            cross.CrossRefSource = (int)CrossRefSource.AniDB;
                            cross.AnimeID = AnimeID;
                            cross.EpisodeID = epid;
                            cross.Percentage = eppp;
                            cross.EpisodeOrder = x + 1;
                            cross.FileName = localFileName;
                            cross.FileSize = FileSize;
                            repFileEpisodes.Save(cross);
                        }
                    }
                }
            }
        }

        public string ToXML()
        {
            var sb = new StringBuilder();
            sb.Append(@"<AniDB_File>");
            sb.Append(string.Format("<ED2KHash>{0}</ED2KHash>", Hash));
            sb.Append(string.Format("<Hash>{0}</Hash>", Hash));
            sb.Append(string.Format("<CRC>{0}</CRC>", CRC));
            sb.Append(string.Format("<MD5>{0}</MD5>", MD5));
            sb.Append(string.Format("<SHA1>{0}</SHA1>", SHA1));
            sb.Append(string.Format("<FileID>{0}</FileID>", FileID));
            sb.Append(string.Format("<AnimeID>{0}</AnimeID>", AnimeID));
            sb.Append(string.Format("<GroupID>{0}</GroupID>", GroupID));
            sb.Append(string.Format("<File_LengthSeconds>{0}</File_LengthSeconds>", File_LengthSeconds));
            sb.Append(string.Format("<File_Source>{0}</File_Source>", File_Source));
            sb.Append(string.Format("<File_AudioCodec>{0}</File_AudioCodec>", File_AudioCodec));
            sb.Append(string.Format("<File_VideoCodec>{0}</File_VideoCodec>", File_VideoCodec));
            sb.Append(string.Format("<File_VideoResolution>{0}</File_VideoResolution>", File_VideoResolution));
            sb.Append(string.Format("<File_FileExtension>{0}</File_FileExtension>", File_FileExtension));
            sb.Append(string.Format("<File_Description>{0}</File_Description>", File_Description));
            sb.Append(string.Format("<FileName>{0}</FileName>", FileName));
            sb.Append(string.Format("<File_ReleaseDate>{0}</File_ReleaseDate>", File_ReleaseDate));
            sb.Append(string.Format("<Anime_GroupName>{0}</Anime_GroupName>", Anime_GroupName));
            sb.Append(string.Format("<Anime_GroupNameShort>{0}</Anime_GroupNameShort>", Anime_GroupNameShort));
            sb.Append(string.Format("<Episode_Rating>{0}</Episode_Rating>", Episode_Rating));
            sb.Append(string.Format("<Episode_Votes>{0}</Episode_Votes>", Episode_Votes));
            sb.Append(string.Format("<DateTimeUpdated>{0}</DateTimeUpdated>", DateTimeUpdated));
            sb.Append(string.Format("<EpisodesRAW>{0}</EpisodesRAW>", EpisodesRAW));
            sb.Append(string.Format("<SubtitlesRAW>{0}</SubtitlesRAW>", SubtitlesRAW));
            sb.Append(string.Format("<LanguagesRAW>{0}</LanguagesRAW>", LanguagesRAW));
            sb.Append(string.Format("<EpisodesPercentRAW>{0}</EpisodesPercentRAW>", EpisodesPercentRAW));
            sb.Append(@"</AniDB_File>");

            return sb.ToString();
        }

        #region DB columns

        public int AniDB_FileID { get; private set; }
        public int FileID { get; set; }
        public string Hash { get; set; }
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
        public int IsWatched { get; set; }
        public DateTime? WatchedDate { get; set; }
        public string CRC { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int FileVersion { get; set; }
        public int IsCensored { get; set; }
        public int IsDeprecated { get; set; }
        public int InternalVersion { get; set; }

        #endregion
    }
}