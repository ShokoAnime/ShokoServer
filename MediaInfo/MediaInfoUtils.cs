using System;
using System.Collections.Generic;
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
    }

    public static class LegacyMediaUtils
    {
        public static string TranslateCodec(string codec)
        {
            if (codec == null) return null;
            codec = codec.ToLowerInvariant();
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
            VideoStream m = c.VideoStream;
            PlexAndKodi.Stream s = new PlexAndKodi.Stream
            {
                Id = m.ID,
                Codec = TranslateCodec(m.Codec),
                CodecID = m.CodecID,
                StreamType = 1,
                Width = m.Width,
                Height = m.Height,
                Duration = (long) c.GeneralStream.Duration,
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
                    (byte) (m.MuxingMode.IndexOf("strip", StringComparison.InvariantCultureIgnoreCase) != -1
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
            foreach (AudioStream m in c.AudioStreams)
            {
                PlexAndKodi.Stream s = new PlexAndKodi.Stream
                {
                    Id = m.ID,
                    CodecID = m.CodecID,
                    Codec = TranslateCodec(m.Codec),
                    Title = m.Title,
                    StreamType = 2,
                    LanguageCode = m.LanguageCode,
                    Language = m.LanguageName,
                    Duration = (int) c.GeneralStream.Duration,
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
                    Forced = (byte) (t.m.Forced ? 1 : 0)
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

        private static readonly Dictionary<string, string> CodecIDs = new Dictionary<string, string>
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
            {"div3", "msmpeg4"},
            {"divx", "mpeg4"},
            {"dts", "dca"},
            {"dts-hd", "dca"},
            {"dx50", "mpeg4"},
            {"flv1", "flv"},
            {"mp42", "msmpeg4v2"},
            {"mp43", "msmpeg4"},
            {"mpa1l2", "mp2"},
            {"mpa1l3", "mp3"},
            {"mpa2.5l3", "mp3"},
            {"mpa2l3", "mp3"},
            {"mpeg-1v", "mpeg1video"},
            {"mpeg-2v", "mpeg2video"},
            {"mpeg-4v", "mpeg4"},
            {"mpg4", "msmpeg4v1"},
            {"on2 vp6", "vp6f"},
            {"sorenson h263", "flv"},
            {"v_mpeg2", "mpeg2"},
            {"v_mpeg4/iso/asp", "mpeg4"},
            {"v_mpeg4/iso/avc", "h264"},
            {"vc-1", "vc1"},
            {"xvid", "mpeg4"}
        };
    }
}