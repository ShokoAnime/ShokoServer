using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models
{
    public class SVR_AniDB_File : AniDB_File, IAniDBFile
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        [XmlIgnore]
        public List<Language> Languages => RepoFactory.CrossRef_Languages_AniDB_File.GetByFileID(FileID).Select(crossref => RepoFactory.Language.GetByID(crossref.LanguageID)).Where(lan => lan != null).ToList();

        [XmlIgnore]
        public List<Language> Subtitles => RepoFactory.CrossRef_Subtitles_AniDB_File.GetByFileID(FileID).Select(crossref => RepoFactory.Language.GetByID(crossref.LanguageID)).Where(sub => sub != null).ToList();

        [XmlIgnore]
        public List<int> EpisodeIDs => RepoFactory.CrossRef_File_Episode.GetByHash(Hash).Select(crossref => crossref.EpisodeID).ToList();

        [XmlIgnore]
        public List<AniDB_Episode> Episodes => RepoFactory.CrossRef_File_Episode.GetByHash(Hash).Where(crossref => crossref.GetEpisode() != null).Select(crossref => crossref.GetEpisode()).ToList();

        [XmlIgnore]
        public List<CrossRef_File_Episode> EpisodeCrossRefs => RepoFactory.CrossRef_File_Episode.GetByHash(Hash);


        public string SubtitlesRAW
        {
            get
            {
                var ret = string.Empty;
                foreach (var lang in Subtitles)
                {
                    if (ret.Length > 0)
                        ret += ",";
                    ret += lang.LanguageName;
                }
                return ret;
            }
        }


        public string LanguagesRAW
        {
            get
            {
                var ret = string.Empty;
                foreach (var lang in Languages)
                {
                    if (ret.Length > 0)
                        ret += ",";
                    ret += lang.LanguageName;
                }
                return ret;
            }
        }

        public static void PopulateHashes(SVR_AniDB_File file, IHashes hashes)
        {
            file.CRC = hashes.CRC ?? "";
            file.MD5 = hashes.MD5;
            file.SHA1 = hashes.SHA1;
            file.Hash = hashes.ED2K;
        }

        public void CreateLanguages(ResponseGetFile response)
        {
            var apostrophe = '\'';

            if ((response?.AudioLanguages?.Count ?? 0) > 0) //Only create relations if the origin of the data if from Raw (WebService/AniDB)
            {
                // Delete old if changed
                var fileLanguages = RepoFactory.CrossRef_Languages_AniDB_File.GetByFileID(FileID);
                foreach (var fLan in fileLanguages)
                {
                    RepoFactory.CrossRef_Languages_AniDB_File.Delete(fLan.CrossRef_Languages_AniDB_FileID);
                }

                foreach (var language in response.AudioLanguages)
                {
                    var rlan = language.Trim().ToLower();
                    if (rlan.Length <= 0) continue;
                    var lan = RepoFactory.Language.GetByLanguageName(rlan);
                    if (lan == null)
                    {
                        lan = new Language { LanguageName = rlan };
                        RepoFactory.Language.Save(lan);
                    }

                    var cross = new CrossRef_Languages_AniDB_File { LanguageID = lan.LanguageID, FileID = FileID };
                    RepoFactory.CrossRef_Languages_AniDB_File.Save(cross);
                }
            }

            if ((response?.SubtitleLanguages?.Count ?? 0) > 0)
            {
                // Delete old if changed
                var fileSubtitles = RepoFactory.CrossRef_Subtitles_AniDB_File.GetByFileID(FileID);
                foreach (var fSub in fileSubtitles)
                {
                    RepoFactory.CrossRef_Subtitles_AniDB_File.Delete(fSub.CrossRef_Subtitles_AniDB_FileID);
                }

                foreach (var language in response.SubtitleLanguages)
                {
                    var rlan = language.Trim().ToLower();
                    if (rlan.Length <= 0) continue;
                    var lan = RepoFactory.Language.GetByLanguageName(rlan);
                    if (lan == null)
                    {
                        lan = new Language { LanguageName = rlan };
                        RepoFactory.Language.Save(lan);
                    }

                    var cross = new CrossRef_Subtitles_AniDB_File { LanguageID = lan.LanguageID, FileID = FileID };
                    RepoFactory.CrossRef_Subtitles_AniDB_File.Save(cross);
                }
            }
        }

        public void CreateCrossEpisodes(string localFileName, ResponseGetFile response)
        {
            if (response.EpisodeIDs.Count <= 0) return;
            var fileEps = RepoFactory.CrossRef_File_Episode.GetByHash(Hash);

            // Use a single session A. for efficiency and B. to prevent regenerating stats
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            using (var trans = session.BeginTransaction())
            {
                RepoFactory.CrossRef_File_Episode.DeleteWithOpenTransaction(session, fileEps);
                trans.Commit();
            }

            fileEps = response.EpisodeIDs.Select(t => response.EpisodeIDs[0])
                .Select(
                    (ep, x) => new CrossRef_File_Episode
                    {
                        Hash = Hash,
                        CrossRefSource = (int)CrossRefSource.AniDB,
                        AnimeID = AnimeID,
                        EpisodeID = ep.EpisodeID,
                        Percentage = ep.Percentage,
                        EpisodeOrder = x + 1,
                        FileName = localFileName,
                        FileSize = FileSize,
                    }
                )
                .ToList();

            // There is a chance that AniDB returned a dup, however unlikely
            using (var trans = session.BeginTransaction())
            {
                RepoFactory.CrossRef_File_Episode.SaveWithOpenTransaction(session,
                    fileEps.DistinctBy(a => $"{a.Hash}-{a.EpisodeID}").ToList());
                trans.Commit();
            }
        }

        public static TitleLanguage GetLanguage(string language)
        {
            switch (language)
            {
                case "afrikaans": return TitleLanguage.Afrikaans;
                case "albanian": return TitleLanguage.Albanian;
                case "arabic": return TitleLanguage.Arabic;
                case "basque": return TitleLanguage.Basque;
                case "bengali": return TitleLanguage.Bengali;
                case "bosnian": return TitleLanguage.Bosnian;
                case "bulgarian": return TitleLanguage.Bulgarian;
                case "czech": return TitleLanguage.Czech;
                case "danish": return TitleLanguage.Danish;
                case "dutch": return TitleLanguage.Dutch;
                case "english": return TitleLanguage.English;
                case "estonian": return TitleLanguage.Estonian;
                case "finnish": return TitleLanguage.Finnish;
                case "french": return TitleLanguage.French;
                case "german": return TitleLanguage.German;
                case "greek (ancient)":
                case "greek": 
                    return TitleLanguage.Greek;
                case "hebrew": return TitleLanguage.Hebrew;
                case "hungarian": return TitleLanguage.Hungarian;
                case "javanese":
                case "malay":
                case "indonesian":
                    return TitleLanguage.Malaysian;
                case "latin": return TitleLanguage.Latin;
                case "italian": return TitleLanguage.Italian;
                case "korean": return TitleLanguage.Korean;
                case "icelandic": 
                case "norwegian": return TitleLanguage.Norwegian;
                case "polish": return TitleLanguage.Polish;
                case "portuguese": return TitleLanguage.Portuguese;
                case "portuguese (brazilian)": return TitleLanguage.BrazilianPortuguese;
                case "romanian": return TitleLanguage.Romanian;
                case "russian": return TitleLanguage.Russian;
                case "slovenian": return TitleLanguage.Slovenian;
                case "ukrainian": return TitleLanguage.Ukrainian;
                case "swedish": return TitleLanguage.Swedish;
                case "thai (transcription)":
                case "thai":
                    return TitleLanguage.Thai;
                case "turkish": return TitleLanguage.Turkish;
                case "vietnamese": return TitleLanguage.Vietnamese;
                case "chinese (simplified)": return TitleLanguage.ChineseSimplified;
                case "chinese (traditional)": return TitleLanguage.ChineseTraditional;
                case "chinese (cantonese)":
                case "chinese (mandarin)":
                case "chinese (unspecified)":
                case "taiwanese":
                    return TitleLanguage.Chinese;
                case "chinese (transcription)": return TitleLanguage.Pinyin;
                case "japanese": return TitleLanguage.Japanese;
                case "japanese (transcription)": return TitleLanguage.Romaji;
                case "catalan":
                case "spanish":
                case "spanish (latin american)":
                    return TitleLanguage.Spanish;
                default:
                    return TitleLanguage.Unknown;
                // TODO these, a proper language class, idk I'm bored an this is tedious
                /*
                 croatian	hr	both	20	0
                esperanto	eo	both	24	0
                filipino	tl	both	26	0
                filipino (tagalog)	tl	both	27	0
                galician	gl	both	30	0
                georgian	ka	both	31	0
                haitian creole	ht	both	35	0
                hindi	hi	both	37	0
                icelandic	is	both	39	0
                17	korean	ko	both	44	0
                87	korean (transcription)	x-kot	written	45	0
                69	latin	la	both	46	0
                67	latvian	lv	both	47	0
                35	lithuanian	lt	both	48	0
                94	mongolian	mn	both	50	0
                91	persian	fa	both	53	0
                serbian	sr	both	59	0
                slovak	sk	both	61	0
                telugu	te	both	69	0
                urdu	ur	written	74	0
                */
            }
        }

        int IAniDBFile.AniDBFileID => FileID;

        IReleaseGroup IAniDBFile.ReleaseGroup => new AniDB_ReleaseGroup
            {GroupName = Anime_GroupName, GroupNameShort = Anime_GroupNameShort};

        string IAniDBFile.Source => File_Source;
        string IAniDBFile.Description => File_Description;
        string IAniDBFile.OriginalFilename => FileName;
        DateTime? IAniDBFile.ReleaseDate => DateTime.UnixEpoch.AddSeconds(File_ReleaseDate);
        int IAniDBFile.Version => FileVersion;
        bool IAniDBFile.Censored => IsCensored ?? false;
        AniDBMediaData IAniDBFile.MediaInfo => new AniDBMediaData
        {
            VideoCodec = File_VideoCodec,
            AudioCodecs = File_AudioCodec.Split("'", StringSplitOptions.RemoveEmptyEntries),
            AudioLanguages = Languages.Select(a => GetLanguage(a.LanguageName)).ToList(),
            SubLanguages = Subtitles.Select(a => GetLanguage(a.LanguageName)).ToList()
        };
    }
}
