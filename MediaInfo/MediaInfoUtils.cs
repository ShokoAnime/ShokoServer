using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace Shoko.Models.MediaInfo
{
    public static class MediaInfoUtils
    {
        public static readonly Dictionary<int, string> ResolutionArea;
        public static readonly Dictionary<int, string> ResolutionArea43;
        public static readonly Dictionary<int, string> ResolutionArea219;

        static MediaInfoUtils()
        {
            ResolutionArea219 = new Dictionary<int, string>
            {
                {2560 * 1080, "UWHD"},
                {3440 * 1440, "UWQHD"}
            };

            ResolutionArea = new Dictionary<int, string>
            {
                {3840 * 2160, "2160p"},
                {2560 * 1440, "1440p"},
                {1920 * 1080, "1080p"},
                {1280 * 720, "720p"},
                {1024 * 576, "576p"},
                {853 * 480, "480p"}
            };

            ResolutionArea43 = new Dictionary<int, string>
            {
                {720 * 576, "576p"},
                {720 * 480, "480p"},
                {480 * 360, "360p"},
                {320 * 240, "240p"}
            };
        }

        const double sixteenNine = 1.777778;
        const double fourThirds = 1.333333;
        const double twentyOneNine = 2.333333;

        private static readonly double[] Ratios =
        {
            fourThirds, sixteenNine, twentyOneNine
        };

        public static string GetStandardResolution(Tuple<int, int> res)
        {
            if (res == null) return null;
            // not precise, but we are rounding and calculating distance anyway
            double ratio = (double) res.Item1 / res.Item2;

            double nearest = FindClosest(Ratios, ratio);

            switch (nearest)
            {
                default:
                {
                    int area = res.Item1 * res.Item2;
                    int key = FindClosest(ResolutionArea.Keys.ToArray(), area);
                    return ResolutionArea[key];
                }
                case fourThirds:
                {
                    int area = res.Item1 * res.Item2;
                    int key = FindClosest(ResolutionArea43.Keys.ToArray(), area);
                    return ResolutionArea43[key];
                }
                case twentyOneNine:
                {
                    int area = res.Item1 * res.Item2;
                    int key = FindClosest(ResolutionArea219.Keys.ToArray(), area);
                    return ResolutionArea219[key];
                }
            }
        }

        private static long FindClosest(IEnumerable<long> array, long value)
        {
            return array.Aggregate((current, next) =>
                Math.Abs(current - value) < Math.Abs(next - value) ? current : next);
        }

        private static int FindClosest(IEnumerable<int> array, int value)
        {
            return array.Aggregate((current, next) =>
                Math.Abs(current - value) < Math.Abs(next - value) ? current : next);
        }

        private static double FindClosest(IEnumerable<double> array, double value)
        {
            return array.Aggregate((current, next) =>
                Math.Abs(current - value) < Math.Abs(next - value) ? current : next);
        }
        
        [SuppressMessage("ReSharper", "StringLiteralTypo")] 
        public static readonly Dictionary<string, Tuple<string, string>> LanguageMapping_2_1_Name = new Dictionary<string, Tuple<string, string>>
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
            {"mul", Tuple.Create("mis", "Multiple Languages")},
            {"sgn", Tuple.Create("sgn", "Signs")},
        };

        public static readonly ILookup<string, Tuple<string, string>> LanguageMapping_1_2_Name =
            LanguageMapping_2_1_Name.ToLookup(a => a.Value.Item1, a => Tuple.Create(a.Key, a.Value.Item2));
        
        public static readonly ILookup<string, Tuple<string, string>> LanguageMapping_Name_2_1 =
            LanguageMapping_2_1_Name.ToLookup(a => a.Value.Item2.ToLowerInvariant(), a => Tuple.Create(a.Key, a.Value.Item1));

        public static string GetLanguageFromCode(string code)
        {
            if (LanguageMapping_1_2_Name.Contains(code)) return LanguageMapping_1_2_Name[code].FirstOrDefault()?.Item1;
            return null;
        }
        
        public static string GetLanguageFromName(string code)
        {
            code = code.ToLowerInvariant();
            if (LanguageMapping_Name_2_1.Contains(code)) return LanguageMapping_Name_2_1[code].FirstOrDefault()?.Item1;
            return null;
        }

        public static string GetLanguageCode(string language)
        {
            if (LanguageMapping_2_1_Name.ContainsKey(language)) return LanguageMapping_2_1_Name[language].Item1;
            return null;
        }

        public static string GetLanguageName(string language)
        {
            if (LanguageMapping_2_1_Name.ContainsKey(language)) return LanguageMapping_2_1_Name[language].Item1;
            return null;
        }

        public static Tuple<string, string> GetLanguageMapping(string language)
        {
            if (LanguageMapping_2_1_Name.ContainsKey(language)) return LanguageMapping_2_1_Name[language];
            return null;
        }
    }

    public static class LegacyMediaUtils
    {
        public static string TranslateCodec(Stream stream)
        {
            if (stream?.Codec == null && stream?.CodecID == null) return null;
            string codec = stream.Codec?.ToLowerInvariant() ?? stream.CodecID?.ToLowerInvariant();
            return CodecIDs.ContainsKey(codec) ? CodecIDs[codec] : codec;
        }

        public static string TranslateFrameRate(MediaContainer m)
        {
            if ((m.VideoStream?.FrameRate ?? 0) == 0) return null;
            float fr = System.Convert.ToSingle(m.VideoStream.FrameRate, CultureInfo.InvariantCulture);
            string frs = ((int) Math.Round(fr)).ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(m.VideoStream.ScanType))
            {
                if (m.VideoStream.ScanType.ToLower().Contains("int"))
                    frs += "i";
                else
                    frs += "p";
            }
            else
                frs += "p";

            switch (frs)
            {
                case "25p":
                case "25i":
                    frs = "PAL";
                    break;
                case "30p":
                case "30i":
                    frs = "NTSC";
                    break;
            }

            return frs;
        }

        private static string TranslateProfile(string codec, string profile)
        {
            profile = profile.ToLowerInvariant();

            if (profile.Contains("advanced simple"))
                return "asp";
            if (codec == "mpeg4" && profile.Equals("simple"))
                return "sp";
            if (profile.StartsWith("m"))
                return "main";
            if (profile.StartsWith("s"))
                return "simple";
            if (profile.StartsWith("a"))
                return "advanced";
            return profile;
        }

        public static string TranslateLevel(string level)
        {
            level = level.Replace(".", string.Empty).ToLowerInvariant();
            if (level.StartsWith("l"))
            {
                int.TryParse(level.Substring(1), out int lll);
                if (lll != 0)
                    level = lll.ToString(CultureInfo.InvariantCulture);
                else if (level.StartsWith("lm"))
                    level = "medium";
                else if (level.StartsWith("lh"))
                    level = "high";
                else
                    level = "low";
            }
            else if (level.StartsWith("m"))
                level = "medium";
            else if (level.StartsWith("h"))
                level = "high";

            return level;
        }

        public static string TranslateContainer(string container)
        {
            if (container == null) return null;
            container = container.ToLowerInvariant();
            foreach (string k in FileContainers.Keys)
            {
                if (container.Contains(k))
                {
                    return FileContainers[k];
                }
            }

            return container;
        }

        public static PlexAndKodi.Stream TranslateVideoStream(MediaContainer c)
        {
            VideoStream m = c?.VideoStream;
            if (m == null) return null;
            PlexAndKodi.Stream s = new PlexAndKodi.Stream
            {
                Id = m.ID,
                Codec = TranslateCodec(m),
                CodecID = m.CodecID,
                StreamType = 1,
                Width = m.Width,
                Height = m.Height,
                Duration = (long) (c.GeneralStream?.Duration * 1000 ?? 0),
                Title = m.Title,
                LanguageCode = m.LanguageCode,
                Language = m.LanguageName,
                BitDepth = (byte) m.BitDepth,
                Index = (byte) m.ID,
                QPel = (byte) (m.Format_Settings_QPel ? 1 : 0),
                GMC = m.Format_Settings_GMC,
                Default = (byte) (m.Default ? 1 : 0),
                Forced = (byte) (m.Forced ? 1 : 0),
                PA = (float) m.PixelAspectRatio,
                Bitrate = (int) Math.Round(m.BitRate / 1000F),
                ScanType = m.ScanType?.ToLower(),
                RefFrames = (byte) m.Format_Settings_RefFrames,
                HeaderStripping =
                    (byte) (m.MuxingMode?.IndexOf("strip", StringComparison.InvariantCultureIgnoreCase) != -1
                        ? 1
                        : 0),
                Cabac = (byte) (m.Format_Settings_CABAC ? 1 : 0),
                FrameRateMode = m.FrameRate_Mode?.ToLower(CultureInfo.InvariantCulture),
                FrameRate = (float) m.FrameRate,
                ColorSpace = m.ColorSpace?.ToLower(CultureInfo.InvariantCulture),
                ChromaSubsampling = m.ChromaSubsampling?.ToLower(CultureInfo.InvariantCulture),
            };

            s.HasScalingMatrix = (byte) (s.Codec == "h264" && s.Level == 31 && s.Cabac == 0 ? 1 : 0);
            s.BVOP = (byte) (m.Format_Settings_BVOP && s.Codec != "mpeg1video" ? 1 : 0);

            if (s.PA != 1.0 && s.Width != 0)
            {
                if (s.Width != 0)
                {
                    float width = s.Width;
                    width *= s.PA;
                    s.PixelAspectRatio = $"{(int) Math.Round(width)}:{s.Width}";
                }
            }


            string fProfile = m.Format_Profile;
            if (!string.IsNullOrEmpty(fProfile))
            {
                int a = fProfile.ToLower(CultureInfo.InvariantCulture).IndexOf("@", StringComparison.Ordinal);
                if (a > 0)
                {
                    s.Profile = TranslateProfile(s.Codec,
                        fProfile.ToLower(CultureInfo.InvariantCulture).Substring(0, a));
                    if (int.TryParse(
                        TranslateLevel(fProfile.ToLower(CultureInfo.InvariantCulture).Substring(a + 1)),
                        out int level)) s.Level = level;
                }
                else
                    s.Profile = TranslateProfile(s.Codec, fProfile.ToLower(CultureInfo.InvariantCulture));
            }

            // Removed Rotation, as MediaInfo doesn't actually list it as a possibility

            return s;
        }

        public static List<PlexAndKodi.Stream> TranslateAudioStreams(MediaContainer c)
        {
            List<PlexAndKodi.Stream> output = new List<PlexAndKodi.Stream>();
            if (c == null) return output;
            foreach (AudioStream m in c.AudioStreams)
            {
                PlexAndKodi.Stream s = new PlexAndKodi.Stream
                {
                    Id = m.ID,
                    CodecID = m.CodecID,
                    Codec = TranslateCodec(m),
                    Title = m.Title,
                    StreamType = 2,
                    LanguageCode = m.LanguageCode,
                    Language = m.LanguageName,
                    Duration = (int) c.GeneralStream.Duration * 1000,
                    Index = (byte) m.ID,
                    Bitrate = (int) Math.Round(m.BitRate / 1000F),
                    BitDepth = (byte) m.BitDepth,
                    SamplingRate = m.SamplingRate,
                    Channels = (byte) m.Channels,
                    BitrateMode = m.BitRate_Mode?.ToLower(CultureInfo.InvariantCulture),
                    DialogNorm = (m.extra?.dialnorm)?.ToString(),
                    Default = (byte) (m.Default ? 1 : 0),
                    Forced = (byte) (m.Forced ? 1 : 0)
                };

                string fProfile = m.Format_Profile;
                if (!string.IsNullOrEmpty(fProfile))
                {
                    if ((fProfile.ToLower() != "layer 3") && (fProfile.ToLower() != "dolby digital") &&
                        (fProfile.ToLower() != "pro") &&
                        (fProfile.ToLower() != "layer 2"))
                        s.Profile = fProfile.ToLower(CultureInfo.InvariantCulture);
                    if (fProfile.ToLower().StartsWith("ma"))
                        s.Profile = "ma";
                }

                string fSettings = m.Format_Settings;
                if (!string.IsNullOrEmpty(fSettings))
                {
                    switch (fSettings)
                    {
                        case "Little / Signed" when s.Codec == "pcm" && m.BitDepth == 16:
                            s.Profile = "pcm_s16le";
                            break;
                        case "Big / Signed" when s.Codec == "pcm" && m.BitDepth == 16:
                            s.Profile = "pcm_s16be";
                            break;
                        case "Little / Unsigned" when s.Codec == "pcm" && m.BitDepth == 8:
                            s.Profile = "pcm_u8";
                            break;
                    }
                }

                output.Add(s);
            }

            return output;
        }

        public static IEnumerable<PlexAndKodi.Stream> TranslateTextStreams(MediaContainer c)
        {
            if (c == null) return new List<PlexAndKodi.Stream>();
            return c.TextStreams.Select(m => new {m, subFormat = GetSubFormat(m.CodecID, m.Format)})
                .Select(t => new PlexAndKodi.Stream
                {
                    Id = t.m.ID,
                    CodecID = t.m.CodecID,
                    StreamType = 3,
                    Title = t.m.SubTitle ?? t.m.Title,
                    LanguageCode = t.m.LanguageCode,
                    Language = t.m.LanguageName,
                    Index = (byte) t.m.ID,
                    Format = t.subFormat,
                    Codec = t.subFormat,
                    Default = (byte) (t.m.Default ? 1 : 0),
                    Forced = (byte) (t.m.Forced ? 1 : 0),
                    File = t.m.External ? t.m.Filename : null
                }).ToList();
        }

        private static string GetSubFormat(string codecID, string format)
        {
            if (!string.IsNullOrEmpty(codecID))
            {
                codecID = codecID.ToUpper();
                foreach (string k in SubFormats.Keys.Where(k => codecID.Contains(k.ToUpper())))
                {
                    return SubFormats[k];
                }
            }

            codecID = format;
            return codecID.ToUpper() == "APPLE TEXT" ? "ttxt" : null;
        }

        private static readonly Dictionary<string, string> SubFormats = new Dictionary<string, string>
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

        private static readonly Dictionary<string, string> FileContainers = new Dictionary<string, string>
        {
            {"cdxa/mpeg-ps", "mpeg"},
            {"divx", "avi"},
            {"flash video", "flv"},
            {"mpeg video", "mpeg"},
            {"mpeg-4", "mp4"},
            {"mpeg-ps", "mpeg"},
            {"realmedia", "rm"},
            {"windows media", "asf"},
            {"matroska", "mkv"},
        };

        public static readonly Dictionary<string, string> CodecIDs = new Dictionary<string, string>
        {
            {"161", "wmav2"},
            {"162", "wmapro"},
            {"2", "adpcm_ms"},
            {"55", "mp3"},
            {"a_aac", "aac"},
            {"a_aac/mpeg4/lc/sbr", "aac"},
            {"a_ac3", "ac3"},
            {"a_flac", "flac"},
            {"aac lc", "aac"},
            {"aac lc-sbr", "aac"},
            {"aac lc-sbr-ps", "aac"},
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
            {"xvid", "mpeg4"}
        };
    }
}
