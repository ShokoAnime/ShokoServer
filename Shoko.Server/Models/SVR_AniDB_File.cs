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
        return language switch
        {
            "afrikaans" => TitleLanguage.Afrikaans,
            "albanian" => TitleLanguage.Albanian,
            "arabic" => TitleLanguage.Arabic,
            "basque" or "spanish (basque)" => TitleLanguage.Basque,
            "bengali" => TitleLanguage.Bengali,
            "bosnian" => TitleLanguage.Bosnian,
            "bulgarian" => TitleLanguage.Bulgarian,
            "burmese" => TitleLanguage.MyanmarBurmese,
            "catalan" or "spanish (catalan)" => TitleLanguage.Catalan,
            "chinese (simplified)" => TitleLanguage.ChineseSimplified,
            "chinese (traditional)" => TitleLanguage.ChineseTraditional,
            "chinese" or "chinese (unspecified)" or
                "cantonese" or "chinese (cantonese)" or
                "mandarin" or "chinese (mandarin)" or
                "taiwanese" or "chinese (taiwanese)" => TitleLanguage.Chinese,
            "chinese (transcription)" => TitleLanguage.Pinyin,
            "croatian" => TitleLanguage.Croatian,
            "czech" => TitleLanguage.Czech,
            "danish" => TitleLanguage.Danish,
            "dutch" => TitleLanguage.Dutch,
            "english" => TitleLanguage.English,
            "esperanto" => TitleLanguage.Esperanto,
            "estonian" => TitleLanguage.Estonian,
            "filipino" or "tagalog" or "filipino (tagalog)" => TitleLanguage.Filipino,
            "finnish" => TitleLanguage.Finnish,
            "french" => TitleLanguage.French,
            "galician" or "spanish (galician)" => TitleLanguage.Galician,
            "georgian" => TitleLanguage.Georgian,
            "german" => TitleLanguage.German,
            "greek (ancient)" or "greek" => TitleLanguage.Greek,
            "haitian creole" => TitleLanguage.HaitianCreole,
            "hebrew" => TitleLanguage.Hebrew,
            "hindi" => TitleLanguage.Hindi,
            "hungarian" => TitleLanguage.Hungarian,
            "icelandic" => TitleLanguage.Icelandic,
            "indonesian" => TitleLanguage.Indonesian,
            "italian" => TitleLanguage.Italian,
            "japanese" => TitleLanguage.Japanese,
            "japanese (transcription)" => TitleLanguage.Romaji,
            "javanese" => TitleLanguage.Javanese,
            "korean" => TitleLanguage.Korean,
            "korean (transcription)" => TitleLanguage.KoreanTranscription,
            "latin" => TitleLanguage.Latin,
            "latvian" => TitleLanguage.Latvian,
            "lithuanian" => TitleLanguage.Lithuanian,
            "malay" => TitleLanguage.Malaysian,
            "mongolian" => TitleLanguage.Mongolian,
            "nepali" => TitleLanguage.Nepali,
            "norwegian" => TitleLanguage.Norwegian,
            "persian" => TitleLanguage.Persian,
            "polish" => TitleLanguage.Polish,
            "portuguese" => TitleLanguage.Portuguese,
            "brazilian" or "portuguese (brazilian)" => TitleLanguage.BrazilianPortuguese,
            "romanian" => TitleLanguage.Romanian,
            "russian" => TitleLanguage.Russian,
            "serbian" => TitleLanguage.Serbian,
            "sinhala" => TitleLanguage.Sinhala,
            "slovak" => TitleLanguage.Slovak,
            "slovenian" => TitleLanguage.Slovenian,
            "spanish" or "spanish (latin american)" => TitleLanguage.Spanish,
            "swedish" => TitleLanguage.Swedish,
            "tamil" => TitleLanguage.Tamil,
            "tatar" => TitleLanguage.Tatar,
            "telugu" => TitleLanguage.Telugu,
            "thai (transcription)" => TitleLanguage.ThaiTranscription,
            "thai" => TitleLanguage.Thai,
            "turkish" => TitleLanguage.Turkish,
            "ukrainian" => TitleLanguage.Ukrainian,
            "urdu" => TitleLanguage.Urdu,
            "vietnamese" => TitleLanguage.Vietnamese,
            _ => TitleLanguage.Unknown
        };
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
        "burmese",
        "catalan",
        "chinese",
        "croatian",
        "czech",
        "danish",
        "dutch",
        "english",
        "esperanto",
        "estonian",
        "filipino",
        "tagalog",
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
            "cantonese",
            "mandarin",
            "taiwanese",
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
