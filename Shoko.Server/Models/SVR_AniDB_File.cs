using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_AniDB_File : AniDB_File
    {
        private string episodesPercentRAW;
        private string episodesRAW;

        private string languagesRAW;
        //private static Logger logger = LogManager.GetCurrentClassLogger();

        private string subtitlesRAW;


        [XmlIgnore]
        [NotMapped]
        public List<Language> Languages
        {
            get
            {
                List<Language> lans = new List<Language>();
                List<CrossRef_Languages_AniDB_File> fileLanguages = Repo.CrossRef_Languages_AniDB_File.GetByFileID(FileID);
                foreach (CrossRef_Languages_AniDB_File crossref in fileLanguages)
                {
                    Language lan = Repo.Language.GetByID(crossref.LanguageID);
                    if (lan != null)
                        lans.Add(lan);
                }
                return lans;
            }
        }


        [XmlIgnore]
        [NotMapped]
        public List<Language> Subtitles
        {
            get
            {
                List<Language> subs = new List<Language>();
                List<CrossRef_Subtitles_AniDB_File> fileSubtitles = Repo.CrossRef_Subtitles_AniDB_File.GetByFileID(FileID);
                foreach (CrossRef_Subtitles_AniDB_File crossref in fileSubtitles)
                {
                    Language sub = Repo.Language.GetByID(crossref.LanguageID);
                    if (sub != null)
                        subs.Add(sub);
                }
                return subs;
            }
        }
        [NotMapped]
        [XmlIgnore]
        public List<int> EpisodeIDs
        {
            get
            {
                List<int> ids = new List<int>();
                List<CrossRef_File_Episode> fileEps = Repo.CrossRef_File_Episode.GetByHash(Hash);
                foreach (CrossRef_File_Episode crossref in fileEps)
                    ids.Add(crossref.EpisodeID);

                return ids;
            }
        }

        [XmlIgnore]
        [NotMapped]
        public List<AniDB_Episode> Episodes
        {
            get
            {
                List<AniDB_Episode> eps = new List<AniDB_Episode>();
                List<CrossRef_File_Episode> fileEps = Repo.CrossRef_File_Episode.GetByHash(Hash);
                foreach (CrossRef_File_Episode crossref in fileEps)
                {
                    if (crossref.GetEpisode() != null)
                        eps.Add(crossref.GetEpisode());
                }
                return eps;
            }
        }

        [XmlIgnore]
        [NotMapped]
        public List<CrossRef_File_Episode> EpisodeCrossRefs => Repo.CrossRef_File_Episode.GetByHash(Hash);

        [NotMapped]
        public string SubtitlesRAW
        {
            get
            {
                if (!string.IsNullOrEmpty(subtitlesRAW))
                    return subtitlesRAW;
                string ret = string.Empty;
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

        [NotMapped]
        public string LanguagesRAW
        {
            get
            {
                if (!string.IsNullOrEmpty(languagesRAW))
                    return languagesRAW;
                string ret = string.Empty;
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

        [NotMapped]
        public string EpisodesRAW
        {
            get
            {
                if (!string.IsNullOrEmpty(episodesRAW))
                    return episodesRAW;
                string ret = string.Empty;
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

        [NotMapped]
        public string EpisodesPercentRAW
        {
            get
            {
                if (!string.IsNullOrEmpty(episodesPercentRAW))
                    return episodesPercentRAW;
                string ret = string.Empty;
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
        [NotMapped]
        public string SubtitlesRAWForWebCache
        {
            get
            {
                char apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(subtitlesRAW))
                    return subtitlesRAW;
                string ret = string.Empty;
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

        [NotMapped]
        public string LanguagesRAWForWebCache
        {
            get
            {
                char apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(languagesRAW))
                    return languagesRAW;
                string ret = string.Empty;
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

        [NotMapped]
        public string EpisodesRAWForWebCache
        {
            get
            {
                char apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(episodesRAW))
                    return episodesRAW;
                string ret = string.Empty;
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

        [NotMapped]
        public string EpisodesPercentRAWForWebCache
        {
            get
            {
                char apostrophe = "'".ToCharArray()[0];

                if (!string.IsNullOrEmpty(episodesPercentRAW))
                    return episodesPercentRAW;
                string ret = string.Empty;
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


        public void Populate_RA(Raw_AniDB_File fileInfo)
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
            IsChaptered = fileInfo.IsChaptered;
            InternalVersion = fileInfo.InternalVersion;
            languagesRAW = fileInfo.LanguagesRAW;
            subtitlesRAW = fileInfo.SubtitlesRAW;
            episodesPercentRAW = fileInfo.EpisodesPercentRAW;
            episodesRAW = fileInfo.EpisodesRAW;
        }


        public void CreateLanguages()
        {
            char apostrophe = "'".ToCharArray()[0];
            Dictionary<string,int> ld=new Dictionary<string,int>();
            List<string> audios=new List<string>();
            List<string> subts=new List<string>();
            if (languagesRAW != null && languagesRAW.Trim().Length > 0) //Only create relations if the origin of the data if from Raw (WebService/AniDB)
            {
                string[] langs = languagesRAW.Split(apostrophe);
                foreach (string language in langs)
                {
                    string rlan = language.Trim().ToLower();
                    if (rlan.Length > 0)
                    {
                        if (!ld.ContainsKey(rlan))
                        {
                            Language lan = Repo.Language.GetByLanguageName(rlan) ?? Repo.Language.BeginAdd(new Language {LanguageName = rlan}).Commit();
                            ld.Add(rlan, lan.LanguageID);
                        }

                        audios.Add(rlan);
                    }
                }
            }

            if (subtitlesRAW != null && subtitlesRAW.Trim().Length > 0)
            {
                string[] subs = subtitlesRAW.Split(apostrophe);
                foreach (string language in subs)
                {
                    string rlan = language.Trim().ToLower();
                    if (rlan.Length > 0)
                    {
                        if (!ld.ContainsKey(rlan))
                        {
                            Language lan = Repo.Language.GetByLanguageName(rlan) ?? Repo.Language.BeginAdd(new Language {LanguageName = rlan}).Commit();
                            ld.Add(rlan, lan.LanguageID);
                        }
                        subts.Add(rlan);
                    }
                }
            }

            if (audios.Count > 0)
            {
                using (var upd = Repo.CrossRef_Languages_AniDB_File.BeginBatchUpdate(() => Repo.CrossRef_Languages_AniDB_File.GetByFileID(FileID),true))
                {
                    foreach (string str in audios)
                    {
                        int langid = ld[str];
                        CrossRef_Languages_AniDB_File cross = upd.FindOrCreate(a => a.LanguageID == langid);
                        cross.FileID = FileID;
                        cross.LanguageID = langid;
                        upd.Update(cross);
                    }
                    upd.Commit();
                }
            }

            if (subts.Count > 0)
            {
                using (var upd = Repo.CrossRef_Subtitles_AniDB_File.BeginBatchUpdate(() => Repo.CrossRef_Subtitles_AniDB_File.GetByFileID(FileID),true))
                {
                    foreach (string str in subts)
                    {
                        int langid = ld[str];
                        CrossRef_Subtitles_AniDB_File cross = upd.FindOrCreate(a => a.LanguageID == langid);
                        cross.FileID = FileID;
                        cross.LanguageID = langid;
                        upd.Update(cross);
                    }
                    upd.Commit();
                }
            }           
        }

        public void CreateCrossEpisodes(string localFileName)
        {
            if (episodesRAW == null) return;
            List<CrossRef_File_Episode> fileEps = Repo.CrossRef_File_Episode.GetByHash(Hash);

            foreach (CrossRef_File_Episode fileEp in fileEps)
                Repo.CrossRef_File_Episode.Delete(fileEp.CrossRef_File_EpisodeID);

            fileEps = new List<CrossRef_File_Episode>();

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
                if (epis.Length <= 0) continue;
                if(!int.TryParse(epis, out int epid)) continue;
                if(!int.TryParse(epps, out int eppp)) continue;
                if (epid == 0) continue;
                CrossRef_File_Episode cross = new CrossRef_File_Episode
                {
                    Hash = Hash,
                    CrossRefSource = (int)CrossRefSource.AniDB,
                    AnimeID = AnimeID,
                    EpisodeID = epid,
                    Percentage = eppp,
                    EpisodeOrder = x + 1,
                    FileName = localFileName,
                    FileSize = FileSize
                };
                fileEps.Add(cross);
            }
            // There is a chance that AniDB returned a dup, however unlikely
            fileEps.DistinctBy(a => $"{a.Hash}-{a.EpisodeID}").ForEach(fileEp => Repo.CrossRef_File_Episode.Touch(() => fileEp));
        }
  
        public string ToXML()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(@"<AniDB_File>");
            sb.Append($"<ED2KHash>{Hash}</ED2KHash>");
            sb.Append($"<Hash>{Hash}</Hash>");
            sb.Append($"<CRC>{CRC}</CRC>");
            sb.Append($"<MD5>{MD5}</MD5>");
            sb.Append($"<SHA1>{SHA1}</SHA1>");
            sb.Append($"<FileID>{FileID}</FileID>");
            sb.Append($"<AnimeID>{AnimeID}</AnimeID>");
            sb.Append($"<GroupID>{GroupID}</GroupID>");
            sb.Append($"<File_LengthSeconds>{File_LengthSeconds}</File_LengthSeconds>");
            sb.Append($"<File_Source>{File_Source}</File_Source>");
            sb.Append($"<File_AudioCodec>{File_AudioCodec}</File_AudioCodec>");
            sb.Append($"<File_VideoCodec>{File_VideoCodec}</File_VideoCodec>");
            sb.Append($"<File_VideoResolution>{File_VideoResolution}</File_VideoResolution>");
            sb.Append($"<File_FileExtension>{File_FileExtension}</File_FileExtension>");
            sb.Append($"<File_Description>{File_Description}</File_Description>");
            sb.Append($"<FileName>{FileName}</FileName>");
            sb.Append($"<File_ReleaseDate>{File_ReleaseDate}</File_ReleaseDate>");
            sb.Append($"<Anime_GroupName>{Anime_GroupName}</Anime_GroupName>");
            sb.Append($"<Anime_GroupNameShort>{Anime_GroupNameShort}</Anime_GroupNameShort>");
            sb.Append($"<Episode_Rating>{Episode_Rating}</Episode_Rating>");
            sb.Append($"<Episode_Votes>{Episode_Votes}</Episode_Votes>");
            sb.Append($"<DateTimeUpdated>{DateTimeUpdated}</DateTimeUpdated>");
            sb.Append($"<EpisodesRAW>{EpisodesRAW}</EpisodesRAW>");
            sb.Append($"<SubtitlesRAW>{SubtitlesRAW}</SubtitlesRAW>");
            sb.Append($"<LanguagesRAW>{LanguagesRAW}</LanguagesRAW>");
            sb.Append($"<EpisodesPercentRAW>{EpisodesPercentRAW}</EpisodesPercentRAW>");
            sb.Append(@"</AniDB_File>");

            return sb.ToString();
        }
    }
}
