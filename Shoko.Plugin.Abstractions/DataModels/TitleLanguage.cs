
namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// The language of a title.
/// </summary>
public enum TitleLanguage
{
    /// <summary>
    /// Main.
    /// </summary>
    Main = -2,

    /// <summary>
    /// None.
    /// </summary>
    None = -1,

    /// <summary>
    /// Unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// English (Any).
    /// </summary>
    English = 1,

    /// <summary>
    /// Japanese (Romaji / Transcription).
    /// </summary>
    Romaji,

    /// <summary>
    /// Japanese (Kanji).
    /// </summary>
    Japanese,

    /// <summary>
    /// Afrikaans.
    /// </summary>
    Afrikaans,

    /// <summary>
    /// Arabic.
    /// </summary>
    Arabic,

    /// <summary>
    /// Bangladeshi.
    /// </summary>
    Bangladeshi,

    /// <summary>
    /// Bulgarian.
    /// </summary>
    Bulgarian,

    /// <summary>
    /// French Canadian.
    /// </summary>
    FrenchCanadian,

    /// <summary>
    /// Czech.
    /// </summary>
    Czech,

    /// <summary>
    /// Danish.
    /// </summary>
    Danish,

    /// <summary>
    /// German.
    /// </summary>
    German,

    /// <summary>
    /// Greek.
    /// </summary>
    Greek,

    /// <summary>
    /// Spanish.
    /// </summary>
    Spanish,

    /// <summary>
    /// Estonian.
    /// </summary>
    Estonian,

    /// <summary>
    /// Finnish.
    /// </summary>
    Finnish,

    /// <summary>
    /// French.
    /// </summary>
    French,

    /// <summary>
    /// Galician.
    /// </summary>
    Galician,

    /// <summary>
    /// Hebrew.
    /// </summary>
    Hebrew,

    /// <summary>
    /// Hungarian.
    /// </summary>
    Hungarian,

    /// <summary>
    /// Italian.
    /// </summary>
    Italian,

    /// <summary>
    /// Korean.
    /// </summary>
    Korean,

    /// <summary>
    /// Lithuanian.
    /// </summary>
    Lithuanian,

    /// <summary>
    /// Mongolian.
    /// </summary>
    Mongolian,

    /// <summary>
    /// Malaysian.
    /// </summary>
    Malaysian,

    /// <summary>
    /// Dutch.
    /// </summary>
    Dutch,

    /// <summary>
    /// Norwegian.
    /// </summary>
    Norwegian,

    /// <summary>
    /// Polish.
    /// </summary>
    Polish,

    /// <summary>
    /// Portuguese.
    /// </summary>
    Portuguese,

    /// <summary>
    /// Brazilian Portuguese.
    /// </summary>
    BrazilianPortuguese,

    /// <summary>
    /// Romanian.
    /// </summary>
    Romanian,

    /// <summary>
    /// Russian.
    /// </summary>
    Russian,

    /// <summary>
    /// Slovak.
    /// </summary>
    Slovak,

    /// <summary>
    /// Slovenian.
    /// </summary>
    Slovenian,

    /// <summary>
    /// Serbian.
    /// </summary>
    Serbian,

    /// <summary>
    /// Swedish.
    /// </summary>
    Swedish,

    /// <summary>
    /// Thai.
    /// </summary>
    Thai,

    /// <summary>
    /// Turkish.
    /// </summary>
    Turkish,

    /// <summary>
    /// Ukrainian.
    /// </summary>
    Ukrainian,

    /// <summary>
    /// Vietnamese.
    /// </summary>
    Vietnamese,

    /// <summary>
    /// Chinese (Any).
    /// </summary>
    Chinese,

    /// <summary>
    /// Chinese (Simplified).
    /// </summary>
    ChineseSimplified,

    /// <summary>
    /// Chinese (Traditional).
    /// </summary>
    ChineseTraditional,

    /// <summary>
    /// Chinese (Pinyin / Transcription).
    /// </summary>
    Pinyin,

    /// <summary>
    /// Latin.
    /// </summary>
    Latin,

    /// <summary>
    /// Albanian.
    /// </summary>
    Albanian,

    /// <summary>
    /// Basque.
    /// </summary>
    Basque,

    /// <summary>
    /// Bengali.
    /// </summary>
    Bengali,

    /// <summary>
    /// Bosnian.
    /// </summary>
    Bosnian,

    /// <summary>
    /// Amharic.
    /// </summary>
    Amharic,

    /// <summary>
    /// Armenian.
    /// </summary>
    Armenian,

    /// <summary>
    /// Azerbaijani.
    /// </summary>
    Azerbaijani,

    /// <summary>
    /// Belarusian.
    /// </summary>
    Belarusian,

    /// <summary>
    /// Catalan.
    /// </summary>
    Catalan,

    /// <summary>
    /// Chichewa.
    /// </summary>
    Chichewa,

    /// <summary>
    /// Corsican.
    /// </summary>
    Corsican,

    /// <summary>
    /// Croatian.
    /// </summary>
    Croatian,

    /// <summary>
    /// Divehi.
    /// </summary>
    Divehi,

    /// <summary>
    /// Esperanto.
    /// </summary>
    Esperanto,

    /// <summary>
    /// Fijian.
    /// </summary>
    Fijian,

    /// <summary>
    /// Georgian.
    /// </summary>
    Georgian,

    /// <summary>
    /// Gujarati.
    /// </summary>
    Gujarati,

    /// <summary>
    /// Haitian Creole.
    /// </summary>
    HaitianCreole,

    /// <summary>
    /// Hausa.
    /// </summary>
    Hausa,

    /// <summary>
    /// Icelandic.
    /// </summary>
    Icelandic,

    /// <summary>
    /// Igbo.
    /// </summary>
    Igbo,

    /// <summary>
    /// Indonesian.
    /// </summary>
    Indonesian,

    /// <summary>
    /// Irish.
    /// </summary>
    Irish,

    /// <summary>
    /// Javanese.
    /// </summary>
    Javanese,

    /// <summary>
    /// Kannada.
    /// </summary>
    Kannada,

    /// <summary>
    /// Kazakh.
    /// </summary>
    Kazakh,

    /// <summary>
    /// Khmer.
    /// </summary>
    Khmer,

    /// <summary>
    /// Kurdish.
    /// </summary>
    Kurdish,

    /// <summary>
    /// Kyrgyz.
    /// </summary>
    Kyrgyz,

    /// <summary>
    /// Lao.
    /// </summary>
    Lao,

    /// <summary>
    /// Latvian.
    /// </summary>
    Latvian,

    /// <summary>
    /// Luxembourgish.
    /// </summary>
    Luxembourgish,

    /// <summary>
    /// Macedonian.
    /// </summary>
    Macedonian,

    /// <summary>
    /// Malagasy.
    /// </summary>
    Malagasy,

    /// <summary>
    /// Malayalam.
    /// </summary>
    Malayalam,

    /// <summary>
    /// Maltese.
    /// </summary>
    Maltese,

    /// <summary>
    /// Maori.
    /// </summary>
    Maori,

    /// <summary>
    /// Marathi.
    /// </summary>
    Marathi,

    /// <summary>
    /// Myanmar Burmese.
    /// </summary>
    MyanmarBurmese,

    /// <summary>
    /// Nepali.
    /// </summary>
    Nepali,

    /// <summary>
    /// Oriya.
    /// </summary>
    Oriya,

    /// <summary>
    /// Pashto.
    /// </summary>
    Pashto,

    /// <summary>
    /// Persian.
    /// </summary>
    Persian,

    /// <summary>
    /// Punjabi.
    /// </summary>
    Punjabi,

    /// <summary>
    /// Quechua.
    /// </summary>
    Quechua,

    /// <summary>
    /// Samoan.
    /// </summary>
    Samoan,

    /// <summary>
    /// Scots Gaelic.
    /// </summary>
    ScotsGaelic,

    /// <summary>
    /// Sesotho.
    /// </summary>
    Sesotho,

    /// <summary>
    /// Shona.
    /// </summary>
    Shona,

    /// <summary>
    /// Sindhi.
    /// </summary>
    Sindhi,

    /// <summary>
    /// Sinhala.
    /// </summary>
    Sinhala,

    /// <summary>
    /// Somali.
    /// </summary>
    Somali,

    /// <summary>
    /// Swahili.
    /// </summary>
    Swahili,

    /// <summary>
    /// Tajik.
    /// </summary>
    Tajik,

    /// <summary>
    /// Tamil.
    /// </summary>
    Tamil,

    /// <summary>
    /// Tatar.
    /// </summary>
    Tatar,

    /// <summary>
    /// Telugu.
    /// </summary>
    Telugu,

    /// <summary>
    /// Turkmen.
    /// </summary>
    Turkmen,

    /// <summary>
    /// Uighur.
    /// </summary>
    Uighur,

    /// <summary>
    /// Uzbek.
    /// </summary>
    Uzbek,

    /// <summary>
    /// Welsh.
    /// </summary>
    Welsh,

    /// <summary>
    /// Xhosa.
    /// </summary>
    Xhosa,

    /// <summary>
    /// Yiddish.
    /// </summary>
    Yiddish,

    /// <summary>
    /// Yoruba.
    /// </summary>
    Yoruba,

    /// <summary>
    /// Zulu.
    /// </summary>
    Zulu,

    /// <summary>
    /// Hindi.
    /// </summary>
    Hindi,

    /// <summary>
    /// Filipino.
    /// </summary>
    Filipino,

    /// <summary>
    /// Korean (Transcription).
    /// </summary>
    KoreanTranscription,

    /// <summary>
    /// Thai (Transcription).
    /// </summary>
    ThaiTranscription,

    /// <summary>
    /// Urdu.
    /// </summary>
    Urdu,

    /// <summary>
    /// English (American).
    /// </summary>
    EnglishAmerican,

    /// <summary>
    /// English (British).
    /// </summary>
    EnglishBritish,

    /// <summary>
    /// English (Australian).
    /// </summary>
    EnglishAustralian,

    /// <summary>
    /// English (Canadian).
    /// </summary>
    EnglishCanadian,

    /// <summary>
    /// English (India).
    /// </summary>
    EnglishIndia,

    /// <summary>
    /// English (New Zealand).
    /// </summary>
    EnglishNewZealand,
}
