using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Shoko.Models.MediaInfo
{
    public static class MediaInfoUtils
    {
        private const double SixteenNine = 1.777778;

        private const double FourThirds = 1.333333;

        private const double TwentyOneNine = 2.333333;

        private static readonly double[] _ratios = [FourThirds, SixteenNine, TwentyOneNine];

        public static readonly IReadOnlyDictionary<int, string> ResolutionArea169 = new Dictionary<int, string>
        {
            {3840 * 2160, "2160p"},
            {2560 * 1440, "1440p"},
            {1920 * 1080, "1080p"},
            {1280 * 720, "720p"},
            {1024 * 576, "576p"},
            {853 * 480, "480p"}
        };

        public static readonly IReadOnlyDictionary<int, string> ResolutionArea43 = new Dictionary<int, string>
        {
            {1440 * 1080, "1080p"},
            {960 * 720, "720p"},
            {720 * 576, "576p"},
            {720 * 480, "480p"},
            {480 * 360, "360p"},
            {320 * 240, "240p"}
        };

        public static readonly IReadOnlyDictionary<int, string> ResolutionArea219 = new Dictionary<int, string>
        {
            {2560 * 1080, "UWHD"},
            {3440 * 1440, "UWQHD"}
        };

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        private static readonly Dictionary<string, Tuple<string, string>> _languageMapping_2_1_Name = new()
        {
            {"aa", Tuple.Create("aar", "Afar")},
            {"ab", Tuple.Create("abk", "Abkhazian")},
            {"ae", Tuple.Create("ave", "Avestan")},
            {"af", Tuple.Create("afr", "Afrikaans")},
            {"ak", Tuple.Create("aka", "Akan")},
            {"am", Tuple.Create("amh", "Amharic")},
            {"an", Tuple.Create("arg", "Aragonese")},
            {"ar", Tuple.Create("ara", "Arabic")},
            {"as", Tuple.Create("asm", "Assamese")},
            {"av", Tuple.Create("ava", "Avaric")},
            {"ay", Tuple.Create("aym", "Aymara")},
            {"az", Tuple.Create("aze", "Azerbaijani")},
            {"ba", Tuple.Create("bak", "Bashkir")},
            {"be", Tuple.Create("bel", "Belarusian")},
            {"bg", Tuple.Create("bul", "Bulgarian")},
            {"bh", Tuple.Create("bih", "Bihari")},
            {"bi", Tuple.Create("bis", "Bislama")},
            {"bm", Tuple.Create("bam", "Bambara")},
            {"bn", Tuple.Create("ben", "Bengali")},
            {"bo", Tuple.Create("tib", "Tibetan")},
            {"br", Tuple.Create("bre", "Breton")},
            {"bs", Tuple.Create("bos", "Bosnian")},
            {"ca", Tuple.Create("cat", "Catalan")},
            {"ce", Tuple.Create("che", "Chechen")},
            {"ch", Tuple.Create("cha", "Chamorro")},
            {"co", Tuple.Create("cos", "Corsican")},
            {"cr", Tuple.Create("cre", "Cree")},
            {"cs", Tuple.Create("cze", "Czech")},
            {"cu", Tuple.Create("chu", "ChurchSlavic")},
            {"cv", Tuple.Create("chv", "Chuvash")},
            {"cy", Tuple.Create("wel", "Welsh")},
            {"da", Tuple.Create("dan", "Danish")},
            {"de", Tuple.Create("ger", "German")},
            {"dv", Tuple.Create("div", "Divehi")},
            {"dz", Tuple.Create("dzo", "Dzongkha")},
            {"ee", Tuple.Create("ewe", "Ewe")},
            {"el", Tuple.Create("ell", "Greek")},
            {"en", Tuple.Create("eng", "English")},
            {"enm", Tuple.Create("enm", "Middle English")},
            {"eo", Tuple.Create("epo", "Esperanto")},
            {"es", Tuple.Create("spa", "Spanish")},
            {"et", Tuple.Create("est", "Estonian")},
            {"eu", Tuple.Create("baq", "Basque")},
            {"fa", Tuple.Create("per", "Persian")},
            {"ff", Tuple.Create("ful", "Fulah")},
            {"fi", Tuple.Create("fin", "Finnish")},
            {"fj", Tuple.Create("fij", "Fijian")},
            {"fo", Tuple.Create("fao", "Faroese")},
            {"fr", Tuple.Create("fre", "French")},
            {"fy", Tuple.Create("fry", "Frisian")},
            {"ga", Tuple.Create("gle", "Irish")},
            {"gd", Tuple.Create("gla", "Gaelic")},
            {"gl", Tuple.Create("glg", "Galician")},
            {"gn", Tuple.Create("grn", "Guarani")},
            {"gu", Tuple.Create("guj", "Gujarati")},
            {"gv", Tuple.Create("glv", "Manx")},
            {"ha", Tuple.Create("hau", "Hausa")},
            {"he", Tuple.Create("heb", "Hebrew")},
            {"hi", Tuple.Create("hin", "Hindi")},
            {"ho", Tuple.Create("hmo", "HiriMotu")},
            {"hr", Tuple.Create("hrv", "Croatian")},
            {"ht", Tuple.Create("hat", "Haitian")},
            {"hu", Tuple.Create("hun", "Hungarian")},
            {"hy", Tuple.Create("arm", "Armenian")},
            {"hz", Tuple.Create("her", "Herero")},
            {"ia", Tuple.Create("ina", "Interlingua")},
            {"id", Tuple.Create("ind", "Indonesian")},
            {"ie", Tuple.Create("ile", "Interlingue")},
            {"ig", Tuple.Create("ibo", "Igbo")},
            {"ii", Tuple.Create("iii", "SichuanYi")},
            {"ik", Tuple.Create("ipk", "Inupiaq")},
            {"io", Tuple.Create("ido", "Ido")},
            {"is", Tuple.Create("ice", "Icelandic")},
            {"it", Tuple.Create("ita", "Italian")},
            {"iu", Tuple.Create("iku", "Inuktitut")},
            {"ja", Tuple.Create("jpn", "Japanese")},
            {"jv", Tuple.Create("jav", "Javanese")},
            {"ka", Tuple.Create("geo", "Georgian")},
            {"kg", Tuple.Create("kon", "Kongo")},
            {"ki", Tuple.Create("kik", "Kikuyu")},
            {"kj", Tuple.Create("kua", "Kuanyama")},
            {"kk", Tuple.Create("kaz", "Kazakh")},
            {"kl", Tuple.Create("kal", "Kalaallisut")},
            {"km", Tuple.Create("khm", "Khmer")},
            {"kn", Tuple.Create("kan", "Kannada")},
            {"ko", Tuple.Create("kor", "Korean")},
            {"kr", Tuple.Create("kau", "Kanuri")},
            {"ks", Tuple.Create("kas", "Kashmiri")},
            {"ku", Tuple.Create("kur", "Kurdish")},
            {"kv", Tuple.Create("kom", "Komi")},
            {"kw", Tuple.Create("cor", "Cornish")},
            {"ky", Tuple.Create("kir", "Kirghiz")},
            {"la", Tuple.Create("lat", "Latin")},
            {"lb", Tuple.Create("ltz", "Luxembourgish")},
            {"lg", Tuple.Create("lug", "Ganda")},
            {"li", Tuple.Create("lim", "Limburgan")},
            {"ln", Tuple.Create("lin", "Lingala")},
            {"lo", Tuple.Create("lao", "Lao")},
            {"lt", Tuple.Create("lit", "Lithuanian")},
            {"lu", Tuple.Create("lub", "LubaKatanga")},
            {"lv", Tuple.Create("lav", "Latvian")},
            {"mg", Tuple.Create("mlg", "Malagasy")},
            {"mh", Tuple.Create("mah", "Marshallese")},
            {"mi", Tuple.Create("mao", "Maori")},
            {"mk", Tuple.Create("mac", "Macedonian")},
            {"ml", Tuple.Create("mal", "Malayalam")},
            {"mn", Tuple.Create("mon", "Mongolian")},
            {"mo", Tuple.Create("mol", "Moldavian")},
            {"mr", Tuple.Create("mar", "Marathi")},
            {"ms", Tuple.Create("may", "Malay")},
            {"mt", Tuple.Create("mlt", "Maltese")},
            {"my", Tuple.Create("bur", "Burmese")},
            {"na", Tuple.Create("nau", "Nauru")},
            {"nb", Tuple.Create("nob", "NorwegianBokmal")},
            {"nd", Tuple.Create("nde", "NorthNdebele")},
            {"ne", Tuple.Create("nep", "Nepali")},
            {"ng", Tuple.Create("ndo", "Ndonga")},
            {"nl", Tuple.Create("dut", "Dutch")},
            {"nn", Tuple.Create("nno", "NorwegianNynorsk")},
            {"no", Tuple.Create("nor", "Norwegian")},
            {"nr", Tuple.Create("nbl", "SouthNdebele")},
            {"nv", Tuple.Create("nav", "Navajo")},
            {"ny", Tuple.Create("nya", "Chichewa")},
            {"oc", Tuple.Create("oci", "Occitan")},
            {"oj", Tuple.Create("oji", "Ojibwa")},
            {"om", Tuple.Create("orm", "Oromo")},
            {"or", Tuple.Create("ori", "Oriya")},
            {"os", Tuple.Create("oss", "Ossetian")},
            {"pa", Tuple.Create("pan", "Panjabi")},
            {"pb", Tuple.Create("pob", "Brazilian")},
            {"pi", Tuple.Create("pli", "Pali")},
            {"pl", Tuple.Create("pol", "Polish")},
            {"ps", Tuple.Create("pus", "Pushto")},
            {"pt", Tuple.Create("por", "Portuguese")},
            {"qu", Tuple.Create("que", "Quechua")},
            {"rm", Tuple.Create("roh", "RaetoRomance")},
            {"rn", Tuple.Create("run", "Rundi")},
            {"ro", Tuple.Create("rum", "Romanian")},
            {"ru", Tuple.Create("rus", "Russian")},
            {"rw", Tuple.Create("kin", "Kinyarwanda")},
            {"sa", Tuple.Create("san", "Sanskrit")},
            {"sc", Tuple.Create("srd", "Sardinian")},
            {"sd", Tuple.Create("snd", "Sindhi")},
            {"se", Tuple.Create("sme", "Sami")},
            {"sg", Tuple.Create("sag", "Sango")},
            {"si", Tuple.Create("sin", "Sinhalese")},
            {"sk", Tuple.Create("slo", "Slovak")},
            {"sl", Tuple.Create("slv", "Slovenian")},
            {"sm", Tuple.Create("smo", "Samoan")},
            {"sn", Tuple.Create("sna", "Shona")},
            {"so", Tuple.Create("som", "Somali")},
            {"sq", Tuple.Create("alb", "Albanian")},
            {"sr", Tuple.Create("srp", "Serbian")},
            {"ss", Tuple.Create("ssw", "Swati")},
            {"st", Tuple.Create("sot", "Sotho")},
            {"su", Tuple.Create("sun", "Sundanese")},
            {"sv", Tuple.Create("swe", "Swedish")},
            {"sw", Tuple.Create("swa", "Swahili")},
            {"ta", Tuple.Create("tam", "Tamil")},
            {"te", Tuple.Create("tel", "Telugu")},
            {"tg", Tuple.Create("tgk", "Tajik")},
            {"th", Tuple.Create("tha", "Thai")},
            {"ti", Tuple.Create("tir", "Tigrinya")},
            {"tk", Tuple.Create("tuk", "Turkmen")},
            {"tl", Tuple.Create("tgl", "Tagalog")},
            {"tn", Tuple.Create("tsn", "Tswana")},
            {"to", Tuple.Create("ton", "Tonga")},
            {"tr", Tuple.Create("tur", "Turkish")},
            {"ts", Tuple.Create("tso", "Tsonga")},
            {"tt", Tuple.Create("tat", "Tatar")},
            {"tw", Tuple.Create("twi", "Twi")},
            {"ty", Tuple.Create("tah", "Tahitian")},
            {"ug", Tuple.Create("uig", "Uighur")},
            {"uk", Tuple.Create("ukr", "Ukrainian")},
            {"ur", Tuple.Create("urd", "Urdu")},
            {"uz", Tuple.Create("uzb", "Uzbek")},
            {"ve", Tuple.Create("ven", "Venda")},
            {"vi", Tuple.Create("vie", "Vietnamese")},
            {"vo", Tuple.Create("vol", "Volapuk")},
            {"wa", Tuple.Create("wln", "Walloon")},
            {"wo", Tuple.Create("wol", "Wolof")},
            {"xh", Tuple.Create("xho", "Xhosa")},
            {"yi", Tuple.Create("yid", "Yiddish")},
            {"yo", Tuple.Create("yor", "Yoruba")},
            {"za", Tuple.Create("zha", "Zhuang")},
            {"zh", Tuple.Create("chi", "Chinese")},
            {"zu", Tuple.Create("zul", "Zulu")},
            {"xx", Tuple.Create("unk", "Unknown")},
            {"zxx", Tuple.Create("unk", "Unknown")},
            {"unk", Tuple.Create("unk", "Unknown")},
            {"mis", Tuple.Create("mis", "Miscoded")},
            {"mul", Tuple.Create("mul", "Multiple Languages")},
            {"sgn", Tuple.Create("sgn", "Signs")},
        };

        private static readonly ILookup<string, Tuple<string, string>> _languageMapping_1_2_Name =
            _languageMapping_2_1_Name.ToLookup(a => a.Value.Item1, a => Tuple.Create(a.Key, a.Value.Item2));

        private static readonly ILookup<string, Tuple<string, string>> _languageMapping_Name_2_1 =
            _languageMapping_2_1_Name.ToLookup(a => a.Value.Item2.ToLowerInvariant(), a => Tuple.Create(a.Key, a.Value.Item1));

        private static readonly Dictionary<string, string> _codecIDs = new()
        {
            {"161", "wmav2"},
            {"162", "wmapro"},
            {"2", "adpcm_ms"},
            {"55", "mp3"},
            {"2000", "ac3"},
            {"a_aac", "aac"},
            {"a_aac-2", "aac"},
            {"a_aac/mpeg4/lc/sbr", "aac"},
            {"a_ac3", "ac3"},
            {"a_flac", "flac"},
            {"aac lc", "aac"},
            {"aac lc-sbr", "aac"},
            {"aac lc-sbr-ps", "aac"},
            {"mp4a", "aac"},
            {"mp4a-40", "aac"},
            {"mp4a-40-2", "aac"},
            {"mp4a-67", "aac"},
            {"mp4a-67-2", "aac"},
            {"a_opus", "opus"},
            {"avc", "h264"},
            {"avc1", "h264"},
            {"div3", "mpeg4"},
            {"divx", "mpeg4"},
            {"dts", "dca"},
            {"dts-hd", "dca"},
            {"dx50", "mpeg4"},
            {"flv1", "flv"},
            {"mp42", "mpeg4"},
            {"mp43", "mpeg4"},
            {"mpa1l2", "mp2"},
            {"a_mpeg/l2", "mp2"},
            {"mp4a-6b", "mp3"},
            {"a_vorbis", "vorbis"},
            {"mpa1l3", "mp3"},
            {"mpa2.5l3", "mp3"},
            {"mpa2l3", "mp3"},
            {"mpeg-1v", "mpeg1"},
            {"mpeg-2v", "mpeg2"},
            {"mpeg-4v", "mpeg4"},
            {"mpg4", "mpeg4"},
            {"on2 vp6", "vp6f"},
            {"sorenson h263", "flv"},
            {"v_mpeg2", "mpeg2"},
            {"v_mpeg4/iso/asp", "mpeg4"},
            {"v_mpeg4/iso/avc", "h264"},
            {"v_mpegh/iso/hevc", "hevc"},
            {"vc-1", "vc1"},
            {"wmv3", "wmv"},
            {"xvid", "mpeg4"}
        };

        private static readonly Dictionary<string, string> _subFormats = new()
        {
            {"c608", "eia-608"},
            {"c708", "eia-708"},
            {"s_ass", "ass"},
            {"s_hdmv/pgs", "pgs"},
            {"s_ssa", "ssa"},
            {"s_text/ass", "ass"},
            {"s_text/ssa", "ssa"},
            {"s_text/usf", "usf"},
            {"s_text/utf8", "srt"},
            {"s_usf", "usf"},
            {"s_vobsub", "vobsub"},
            {"subp", "vobsub"},
            {"s_image/bmp", "bmp"},
        };

        public static string GetLanguageFromCode(string code)
        {
            if (_languageMapping_1_2_Name.Contains(code)) return _languageMapping_1_2_Name[code].FirstOrDefault()?.Item1;
            return null;
        }

        public static string GetLanguageFromName(string code)
        {
            code = code.ToLowerInvariant();
            if (_languageMapping_Name_2_1.Contains(code)) return _languageMapping_Name_2_1[code].FirstOrDefault()?.Item1;
            return null;
        }

        public static Tuple<string, string> GetLanguageMapping(string language)
        {
            if (_languageMapping_2_1_Name.TryGetValue(language, out var value)) return value;
            return null;
        }

        public static string GetStandardResolution(Tuple<int, int> res)
        {
            if (res == null)
                return null;

            // not precise, but we are rounding and calculating distance anyway
            var ratio = (double)res.Item1 / res.Item2;
            var nearest = FindClosestRatio(_ratios, ratio);
            switch (nearest)
            {
                default:
                case SixteenNine:
                    {
                        var area = res.Item1 * res.Item2;
                        var key = FindClosestRatio(ResolutionArea169.Keys, area);
                        return ResolutionArea169[key];
                    }
                case FourThirds:
                    {
                        var area = res.Item1 * res.Item2;
                        var key = FindClosestRatio(ResolutionArea43.Keys, area);
                        return ResolutionArea43[key];
                    }
                case TwentyOneNine:
                    {
                        var area = res.Item1 * res.Item2;
                        var key = FindClosestRatio(ResolutionArea219.Keys, area);
                        return ResolutionArea219[key];
                    }
            }
        }

        private static double FindClosestRatio(IEnumerable<double> array, double value)
        {
            return array.Aggregate((current, next) =>
                Math.Abs(current - value) < Math.Abs(next - value) ? current : next);
        }

        private static int FindClosestRatio(IEnumerable<int> array, int value)
        {
            return array.Aggregate((current, next) =>
                Math.Abs(current - value) < Math.Abs(next - value) ? current : next);
        }

        public static string TranslateCodec(Stream stream)
        {
            if (stream?.Codec == null && stream?.CodecID == null)
                return null;

            var id = stream.Codec?.ToLower();
            if (id != null && _codecIDs.TryGetValue(id, out var value))
                return value.ToUpper();
            if (id != null && id.Contains('/') && _codecIDs.TryGetValue(id.Split('/')[^1].Trim(), out value))
                return value.ToUpper();

            id = stream.CodecID?.ToLower();
            if (id != null && _codecIDs.TryGetValue(id, out value))
                return value.ToUpper();
            if (id != null && id.Contains('/') && _codecIDs.TryGetValue(id.Split('/')[^1].Trim(), out value))
                return value.ToUpper();

            if (stream is TextStream textStream)
            {
                if (!string.IsNullOrEmpty(id) && _subFormats.TryGetValue(id, out var codecFormat))
                    return codecFormat.ToUpper();

                return textStream.Format?.ToLowerInvariant() == "apple text" ? "TTXT" : null;
            }

            return id?.Split('/')[^1].Trim().ToUpper();
        }
    }
}
