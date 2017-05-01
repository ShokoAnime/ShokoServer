using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_AniDB_File : AniDB_File
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private string subtitlesRAW;
        private string languagesRAW;
        private string episodesRAW;
        private string episodesPercentRAW;

        public SVR_AniDB_File() //Empty Constructor for nhibernate
        {
        }

        [XmlIgnore]
        public List<Language> Languages
        {
            get
            {
                List<Language> lans = new List<Language>();

                List<CrossRef_Languages_AniDB_File> fileLanguages =
                    RepoFactory.CrossRef_Languages_AniDB_File.GetByFileID(this.FileID);

                foreach (CrossRef_Languages_AniDB_File crossref in fileLanguages)
                {
                    Language lan = RepoFactory.Language.GetByID(crossref.LanguageID);
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
                List<Language> subs = new List<Language>();

                List<CrossRef_Subtitles_AniDB_File> fileSubtitles =
                    RepoFactory.CrossRef_Subtitles_AniDB_File.GetByFileID(this.FileID);


                foreach (CrossRef_Subtitles_AniDB_File crossref in fileSubtitles)
                {
                    Language sub = RepoFactory.Language.GetByID(crossref.LanguageID);
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
                List<int> ids = new List<int>();

                List<CrossRef_File_Episode> fileEps = RepoFactory.CrossRef_File_Episode.GetByHash(this.Hash);

                foreach (CrossRef_File_Episode crossref in fileEps)
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
                List<AniDB_Episode> eps = new List<AniDB_Episode>();

                List<CrossRef_File_Episode> fileEps = RepoFactory.CrossRef_File_Episode.GetByHash(this.Hash);

                foreach (CrossRef_File_Episode crossref in fileEps)
                {
                    if (crossref.GetEpisode() != null) eps.Add(crossref.GetEpisode());
                }
                return eps;
            }
        }

        [XmlIgnore]
        public List<CrossRef_File_Episode> EpisodeCrossRefs
        {
            get { return RepoFactory.CrossRef_File_Episode.GetByHash(this.Hash); }
        }


        public string SubtitlesRAW
        {
            get
            {
                if (!string.IsNullOrEmpty(subtitlesRAW))
                    return subtitlesRAW;
                string ret = "";
                foreach (Language lang in this.Subtitles)
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
                string ret = "";
                foreach (Language lang in this.Languages)
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
                string ret = "";
                foreach (CrossRef_File_Episode cross in EpisodeCrossRefs)
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
                string ret = "";
                foreach (CrossRef_File_Episode cross in EpisodeCrossRefs)
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
                char apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(subtitlesRAW))
                    return subtitlesRAW;
                string ret = "";
                foreach (Language lang in this.Subtitles)
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
                char apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(languagesRAW))
                    return languagesRAW;
                string ret = "";
                foreach (Language lang in this.Languages)
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
                char apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(episodesRAW))
                    return episodesRAW;
                string ret = "";
                foreach (CrossRef_File_Episode cross in EpisodeCrossRefs)
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
                char apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(episodesPercentRAW))
                    return episodesPercentRAW;
                string ret = "";
                foreach (CrossRef_File_Episode cross in EpisodeCrossRefs)
                {
                    if (ret.Length > 0)
                        ret += apostrophe;
                    ret += cross.Percentage.ToString();
                }
                return ret;
            }
            set { episodesPercentRAW = value; }
        }


        public static void Populate(SVR_AniDB_File anidbfile, Raw_AniDB_File fileInfo)
        {
            anidbfile.Anime_GroupName = fileInfo.Anime_GroupName;
            anidbfile.Anime_GroupNameShort = fileInfo.Anime_GroupNameShort;
            anidbfile.AnimeID = fileInfo.AnimeID;
            anidbfile.CRC = fileInfo.CRC;
            anidbfile.DateTimeUpdated = DateTime.Now;
            anidbfile.Episode_Rating = fileInfo.Episode_Rating;
            anidbfile.Episode_Votes = fileInfo.Episode_Votes;
            anidbfile.File_AudioCodec = fileInfo.File_AudioCodec;
            anidbfile.File_Description = fileInfo.File_Description;
            anidbfile.File_FileExtension = fileInfo.File_FileExtension;
            anidbfile.File_LengthSeconds = fileInfo.File_LengthSeconds;
            anidbfile.File_ReleaseDate = fileInfo.File_ReleaseDate;
            anidbfile.File_Source = fileInfo.File_Source;
            anidbfile.File_VideoCodec = fileInfo.File_VideoCodec;
            anidbfile.File_VideoResolution = fileInfo.File_VideoResolution;
            anidbfile.FileID = fileInfo.FileID;
            anidbfile.FileName = fileInfo.FileName;
            anidbfile.FileSize = fileInfo.FileSize;
            anidbfile.GroupID = fileInfo.GroupID;
            anidbfile.Hash = fileInfo.ED2KHash;
            anidbfile.IsWatched = fileInfo.IsWatched;
            anidbfile.MD5 = fileInfo.MD5;
            anidbfile.SHA1 = fileInfo.SHA1;
            anidbfile.FileVersion = fileInfo.FileVersion;
            anidbfile.IsCensored = fileInfo.IsCensored;
            anidbfile.IsDeprecated = fileInfo.IsDeprecated;
            anidbfile.IsChaptered = fileInfo.IsChaptered;
            anidbfile.InternalVersion = fileInfo.InternalVersion;

            anidbfile.languagesRAW = fileInfo.LanguagesRAW;
            anidbfile.subtitlesRAW = fileInfo.SubtitlesRAW;
            anidbfile.episodesPercentRAW = fileInfo.EpisodesPercentRAW;
            anidbfile.episodesRAW = fileInfo.EpisodesRAW;
        }


        public void CreateLanguages()
        {
            char apostrophe = "'".ToCharArray()[0];

            if (languagesRAW != null) //Only create relations if the origin of the data if from Raw (WebService/AniDB)
            {
                if (languagesRAW.Trim().Length == 0) return;
                // Delete old if changed

                List<CrossRef_Languages_AniDB_File> fileLanguages =
                    RepoFactory.CrossRef_Languages_AniDB_File.GetByFileID(this.FileID);
                foreach (CrossRef_Languages_AniDB_File fLan in fileLanguages)
                {
                    RepoFactory.CrossRef_Languages_AniDB_File.Delete(fLan.CrossRef_Languages_AniDB_FileID);
                }


                string[] langs = languagesRAW.Split(apostrophe);
                foreach (string language in langs)
                {
                    string rlan = language.Trim().ToLower();
                    if (rlan.Length > 0)
                    {
                        Language lan = RepoFactory.Language.GetByLanguageName(rlan);
                        if (lan == null)
                        {
                            lan = new Language();
                            lan.LanguageName = rlan;
                            RepoFactory.Language.Save(lan);
                        }
                        CrossRef_Languages_AniDB_File cross = new CrossRef_Languages_AniDB_File();
                        cross.LanguageID = lan.LanguageID;
                        cross.FileID = FileID;
                        RepoFactory.CrossRef_Languages_AniDB_File.Save(cross);
                    }
                }
            }

            if (subtitlesRAW != null)
            {
                if (subtitlesRAW.Trim().Length == 0) return;

                // Delete old if changed
                List<CrossRef_Subtitles_AniDB_File> fileSubtitles =
                    RepoFactory.CrossRef_Subtitles_AniDB_File.GetByFileID(this.FileID);

                foreach (CrossRef_Subtitles_AniDB_File fSub in fileSubtitles)
                {
                    RepoFactory.CrossRef_Subtitles_AniDB_File.Delete(fSub.CrossRef_Subtitles_AniDB_FileID);
                }

                string[] subs = subtitlesRAW.Split(apostrophe);
                foreach (string language in subs)
                {
                    string rlan = language.Trim().ToLower();
                    if (rlan.Length > 0)
                    {
                        Language lan = RepoFactory.Language.GetByLanguageName(rlan);
                        if (lan == null)
                        {
                            lan = new Language();
                            lan.LanguageName = rlan;
                            RepoFactory.Language.Save(lan);
                        }
                        CrossRef_Subtitles_AniDB_File cross = new CrossRef_Subtitles_AniDB_File();
                        cross.LanguageID = lan.LanguageID;
                        cross.FileID = FileID;
                        RepoFactory.CrossRef_Subtitles_AniDB_File.Save(cross);
                    }
                }
            }
        }

        public void CreateCrossEpisodes(string localFileName)
        {
            if (episodesRAW != null) //Only create relations if the origin of the data if from Raw (AniDB)
            {
                List<CrossRef_File_Episode> fileEps = RepoFactory.CrossRef_File_Episode.GetByHash(this.Hash);

                foreach (CrossRef_File_Episode fileEp in fileEps)
                    RepoFactory.CrossRef_File_Episode.Delete(fileEp.CrossRef_File_EpisodeID);

                char apostrophe = "'".ToCharArray()[0];
                char epiSplit = ',';
                if (episodesRAW.Contains(apostrophe))
                    epiSplit = apostrophe;

                char eppSplit = ',';
                if (episodesPercentRAW.Contains(apostrophe))
                    eppSplit = apostrophe;

                string[] epi = episodesRAW.Split(epiSplit);
                string[] epp = episodesPercentRAW.Split(eppSplit);
                for (int x = 0; x < epi.Length; x++)
                {
                    string epis = epi[x].Trim();
                    string epps = epp[x].Trim();
                    if (epis.Length > 0)
                    {
                        int epid = 0;
                        int.TryParse(epis, out epid);
                        int eppp = 100;
                        int.TryParse(epps, out eppp);
                        if (epid != 0)
                        {
                            CrossRef_File_Episode cross = new CrossRef_File_Episode();
                            cross.Hash = Hash;
                            cross.CrossRefSource = (int) CrossRefSource.AniDB;
                            cross.AnimeID = this.AnimeID;
                            cross.EpisodeID = epid;
                            cross.Percentage = eppp;
                            cross.EpisodeOrder = x + 1;
                            cross.FileName = localFileName;
                            cross.FileSize = FileSize;
                            RepoFactory.CrossRef_File_Episode.Save(cross);
                        }
                    }
                }
            }
        }

        public string ToXML()
        {
            StringBuilder sb = new StringBuilder();
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
    }
}