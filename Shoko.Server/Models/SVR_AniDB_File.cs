using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Serialization;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_AniDB_File : AniDB_File, IAniDBFile
{
    [XmlIgnore]
    public List<CrossRef_Languages_AniDB_File> Languages =>
        RepoFactory.CrossRef_Languages_AniDB_File.GetByFileID(FileID).ToList();

    [XmlIgnore]
    public List<CrossRef_Subtitles_AniDB_File> Subtitles =>
        RepoFactory.CrossRef_Subtitles_AniDB_File.GetByFileID(FileID).ToList();

    [XmlIgnore]
    public List<int> EpisodeIDs => RepoFactory.CrossRef_File_Episode.GetByEd2k(Hash)
        .Select(crossref => crossref.EpisodeID).ToList();

    [XmlIgnore]
    public List<SVR_AniDB_Episode> Episodes => RepoFactory.CrossRef_File_Episode.GetByEd2k(Hash)
        .Select(crossref => crossref.AniDBEpisode)
        .WhereNotNull()
        .OrderBy(ep => ep.EpisodeTypeEnum)
        .OrderBy(ep => ep.EpisodeNumber)
        .ToList();

    [XmlIgnore]
    public IReadOnlyList<SVR_CrossRef_File_Episode> EpisodeCrossReferences => RepoFactory.CrossRef_File_Episode.GetByEd2k(Hash);

    // NOTE: I want to cache it, but i won't for now. not until the anidb files and release groups are stored in a non-cached repo.
    public AniDB_ReleaseGroup ReleaseGroup =>
        RepoFactory.AniDB_ReleaseGroup.GetByGroupID(GroupID) ?? new()
        {
            GroupID = GroupID
        };

    public string Anime_GroupName => ReleaseGroup?.GroupName;

    public string Anime_GroupNameShort => ReleaseGroup?.GroupNameShort;

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
            case "basque":
            case "spanish (basque)":
                return TitleLanguage.Basque;
            case "bengali": return TitleLanguage.Bengali;
            case "bosnian": return TitleLanguage.Bosnian;
            case "bulgarian": return TitleLanguage.Bulgarian;
            case "burmese": return TitleLanguage.MyanmarBurmese;
            case "catalan":
            case "spanish (catalan)":
                return TitleLanguage.Catalan;
            case "chinese (simplified)": return TitleLanguage.ChineseSimplified;
            case "chinese (traditional)": return TitleLanguage.ChineseTraditional;
            case "chinese":
            case "chinese (unspecified)":
            case "cantonese":
            case "chinese (cantonese)":
            case "mandarin":
            case "chinese (mandarin)":
            case "taiwanese":
            case "chinese (taiwanese)":
                return TitleLanguage.Chinese;
            case "chinese (transcription)": return TitleLanguage.Pinyin;
            case "croatian": return TitleLanguage.Croatian;
            case "czech": return TitleLanguage.Czech;
            case "danish": return TitleLanguage.Danish;
            case "dutch": return TitleLanguage.Dutch;
            case "english": return TitleLanguage.English;
            case "esperanto": return TitleLanguage.Esperanto;
            case "estonian": return TitleLanguage.Estonian;
            case "filipino":
            case "tagalog":
            case "filipino (tagalog)":
                return TitleLanguage.Filipino;
            case "finnish": return TitleLanguage.Finnish;
            case "french": return TitleLanguage.French;
            case "galician":
            case "spanish (galician)":
                return TitleLanguage.Galician;
            case "georgian": return TitleLanguage.Georgian;
            case "german": return TitleLanguage.German;
            case "greek (ancient)":
            case "greek":
                return TitleLanguage.Greek;
            case "haitian creole": return TitleLanguage.HaitianCreole;
            case "hebrew": return TitleLanguage.Hebrew;
            case "hindi": return TitleLanguage.Hindi;
            case "hungarian": return TitleLanguage.Hungarian;
            case "icelandic": return TitleLanguage.Icelandic;
            case "indonesian": return TitleLanguage.Indonesian;
            case "italian": return TitleLanguage.Italian;
            case "japanese": return TitleLanguage.Japanese;
            case "japanese (transcription)": return TitleLanguage.Romaji;
            case "javanese": return TitleLanguage.Javanese;
            case "korean": return TitleLanguage.Korean;
            case "korean (transcription)": return TitleLanguage.KoreanTranscription;
            case "latin": return TitleLanguage.Latin;
            case "latvian": return TitleLanguage.Latvian;
            case "lithuanian": return TitleLanguage.Lithuanian;
            case "malay": return TitleLanguage.Malaysian;
            case "mongolian": return TitleLanguage.Mongolian;
            case "nepali": return TitleLanguage.Nepali;
            case "norwegian": return TitleLanguage.Norwegian;
            case "persian": return TitleLanguage.Persian;
            case "polish": return TitleLanguage.Polish;
            case "portuguese": return TitleLanguage.Portuguese;
            case "brazilian":
            case "portuguese (brazilian)":
                return TitleLanguage.BrazilianPortuguese;
            case "romanian": return TitleLanguage.Romanian;
            case "russian": return TitleLanguage.Russian;
            case "serbian": return TitleLanguage.Serbian;
            case "sinhala": return TitleLanguage.Sinhala;
            case "slovak": return TitleLanguage.Slovak;
            case "slovenian": return TitleLanguage.Slovenian;
            case "spanish":
            case "spanish (latin american)":
                return TitleLanguage.Spanish;
            case "swedish": return TitleLanguage.Swedish;
            case "tamil": return TitleLanguage.Tamil;
            case "tatar": return TitleLanguage.Tatar;
            case "telugu": return TitleLanguage.Telugu;
            case "thai (transcription)": return TitleLanguage.ThaiTranscription;
            case "thai": return TitleLanguage.Thai;
            case "turkish": return TitleLanguage.Turkish;
            case "ukrainian": return TitleLanguage.Ukrainian;
            case "urdu": return TitleLanguage.Urdu;
            case "vietnamese": return TitleLanguage.Vietnamese;
            default:
                return TitleLanguage.Unknown;
        }
    }

    private static readonly ImmutableHashSet<string> _possibleLanguagesBoth =
    [
        "afrikaans",
        "albanian",
        "arabic",
        "basque",
        "bengali",
        "bosnian",
        "bulgarian",
        "catalan",
        "chinese",
        "chinese (unspecified)",
        "croatian",
        "czech",
        "danish",
        "dutch",
        "english",
        "esperanto",
        "estonian",
        "filipino",
        "filipino (tagalog)",
        "finnish",
        "french",
        "galician",
        "georgian",
        "german",
        "greek",
        "haitian creole",
        "hebrew",
        "hindi",
        "hungarian",
        "icelandic",
        "indonesian",
        "italian",
        "japanese",
        "javanese",
        "korean",
        "latin",
        "latvian",
        "lithuanian",
        "malay",
        "mongolian",
        "nepali",
        "norwegian",
        "persian",
        "polish",
        "portuguese",
        "portuguese (brazilian)",
        "romanian",
        "russian",
        "serbian",
        "sinhala",
        "slovak",
        "slovenian",
        "spanish",
        "spanish (latin american)",
        "swedish",
        "tamil",
        "tatar",
        "telugu",
        "thai",
        "turkish",
        "ukrainian",
        "vietnamese",
        "unknown",
        "other",
    ];

    private static readonly ImmutableHashSet<string> _possibleAudioLanguages =
        _possibleLanguagesBoth.Union([
            "chinese (cantonese)",
            "chinese (mandarin)",
            "chinese (taiwanese)",
            "instrumental",
        ]);

    public static string[] GetPossibleAudioLanguages()
    {
        return _possibleAudioLanguages.ToArray();
    }

    private static readonly ImmutableHashSet<string> _possibleSubtitleLanguages =
        _possibleLanguagesBoth.Union([
            "chinese (simplified)",
            "chinese (traditional)",
            "chinese (transcription)",
            "greek (ancient)",
            "japanese (transcription)",
            "korean (transcription)",
            "thai (transcription)",
            "urdu",
        ]);

    public static string[] GetPossibleSubtitleLanguages()
    {
        return _possibleSubtitleLanguages.ToArray();
    }

    int IAniDBFile.AniDBFileID => FileID;

    IReleaseGroup IAniDBFile.ReleaseGroup
        => RepoFactory.AniDB_ReleaseGroup.GetByGroupID(GroupID) ?? new() { GroupID = GroupID };

    string IAniDBFile.Source => File_Source;
    string IAniDBFile.Description => File_Description;
    string IAniDBFile.OriginalFilename => FileName;
    DateTime? IAniDBFile.ReleaseDate => DateTime.UnixEpoch.AddSeconds(File_ReleaseDate);
    int IAniDBFile.Version => FileVersion;
    bool IAniDBFile.Censored => IsCensored ?? false;

    AniDBMediaData IAniDBFile.MediaInfo => new()
    {
        AudioLanguages = Languages.Select(a => GetLanguage(a.LanguageName)).ToList(), SubLanguages = Subtitles.Select(a => GetLanguage(a.LanguageName)).ToList()
    };
}
