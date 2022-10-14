using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using NLog;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_AniDB_File : AniDB_File, IAniDBFile
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    [XmlIgnore]
    public List<CrossRef_Languages_AniDB_File> Languages =>
        RepoFactory.CrossRef_Languages_AniDB_File.GetByFileID(FileID).ToList();

    [XmlIgnore]
    public List<CrossRef_Subtitles_AniDB_File> Subtitles =>
        RepoFactory.CrossRef_Subtitles_AniDB_File.GetByFileID(FileID).ToList();

    [XmlIgnore]
    public List<int> EpisodeIDs => RepoFactory.CrossRef_File_Episode.GetByHash(Hash)
        .Select(crossref => crossref.EpisodeID).ToList();

    [XmlIgnore]
    public List<AniDB_Episode> Episodes => RepoFactory.CrossRef_File_Episode.GetByHash(Hash)
        .Select(crossref => crossref.GetEpisode()).Where(ep => ep != null).ToList();

    [XmlIgnore]
    public List<CrossRef_File_Episode> EpisodeCrossRefs => RepoFactory.CrossRef_File_Episode.GetByHash(Hash);

    public string Anime_GroupName => RepoFactory.AniDB_ReleaseGroup.GetByGroupID(GroupID)?.Name;
    public string Anime_GroupNameShort => RepoFactory.AniDB_ReleaseGroup.GetByGroupID(GroupID)?.ShortName;


    public string SubtitlesRAW
    {
        get
        {
            var ret = string.Empty;
            foreach (var lang in Subtitles)
            {
                if (ret.Length > 0)
                {
                    ret += ",";
                }

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
                {
                    ret += ",";
                }

                ret += lang.LanguageName;
            }

            return ret;
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

    IReleaseGroup IAniDBFile.ReleaseGroup
    {
        get
        {
            var group = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(GroupID);
            if (group == null)
            {
                return null;
            }

            return new AniDB_ReleaseGroup { GroupName = group.Name, GroupNameShort = group.ShortName };
        }
    }

    string IAniDBFile.Source => File_Source;
    string IAniDBFile.Description => File_Description;
    string IAniDBFile.OriginalFilename => FileName;
    DateTime? IAniDBFile.ReleaseDate => DateTime.UnixEpoch.AddSeconds(File_ReleaseDate);
    int IAniDBFile.Version => FileVersion;
    bool IAniDBFile.Censored => IsCensored ?? false;

    AniDBMediaData IAniDBFile.MediaInfo => new()
    {
        AudioLanguages = Languages.Select(a => GetLanguage(a.LanguageName)).ToList(),
        SubLanguages = Subtitles.Select(a => GetLanguage(a.LanguageName)).ToList()
    };
}
