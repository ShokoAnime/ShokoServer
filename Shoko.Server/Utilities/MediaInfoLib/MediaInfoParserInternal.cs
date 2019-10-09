using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SeekOrigin = System.IO.SeekOrigin;
using System.Linq;
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using NLog;
using Shoko.Models.PlexAndKodi;
using Stream = Shoko.Models.PlexAndKodi.Stream;


namespace Shoko.Server.Utilities.MediaInfoLib
{
    // ReSharper disable CompareOfFloatsByEqualityOperator

    public static class MediaInfoParserInternal
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static string TranslateCodec(string codec)
        {
            codec = codec.ToLowerInvariant();
            if (CodecIDs.ContainsKey(codec))
                return CodecIDs[codec];
            return codec;
        }

        private static int BiggerFromList(string list)
        {
            int max = 0;
            string[] nams = list.Split('/');
            for (int x = 0; x < nams.Length; x++)
            {
                if (int.TryParse(nams[x].Trim(), out int k))
                {
                    if (k > max)
                        max = k;
                }
            }

            return max;
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

        private static string TranslateLevel(string level)
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

        private static string GetLanguageFromCode3(string code3, string full)
        {
            for (int x = 0; x < languages.GetUpperBound(0); x++)
            {
                if (languages[x, 2] == code3)
                {
                    return languages[x, 0];
                }
            }

            return full;
        }

        public static string PostTranslateCode3(string c)
        {
            c = c.ToLowerInvariant();
            foreach (string k in code3_post.Keys)
            {
                if (c.Contains(k))
                {
                    return code3_post[k];
                }
            }

            return c;
        }

        public static string PostTranslateLan(string c)
        {
            c = c.ToLowerInvariant();
            foreach (string k in lan_post.Keys)
            {
                if (c.Contains(k))
                {
                    return lan_post[k];
                }
            }

            return c;
        }

        private static string TranslateContainer(string container)
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

        private static Stream TranslateVideoStream(MediaInfoDLLInternal m, int num)
        {
            Stream s = new Stream
            {
                Id = m.GetByte(StreamKind.Video, num, "UniqueID"),
                Codec = TranslateCodec(m.Get(StreamKind.Video, num, "Codec")),
                CodecID = m.Get(StreamKind.Video, num, "CodecID"),
                StreamType = 1,
                Width = m.GetInt(StreamKind.Video, num, "Width"),
                Height = m.GetInt(StreamKind.Video, num, "Height"),
                Duration = m.GetInt(StreamKind.Video, num, "Duration")
            };
            string title = m.Get(StreamKind.Video, num, "Title");
            if (!string.IsNullOrEmpty(title))
                s.Title = title;

            string lang = m.Get(StreamKind.Video, num, "Language/String3");
            if (!string.IsNullOrEmpty(lang))
                s.LanguageCode = PostTranslateCode3(lang);
            string lan = PostTranslateLan(GetLanguageFromCode3(lang, m.Get(StreamKind.Video, num, "Language/String1")));
            if (!string.IsNullOrEmpty(lan))
                s.Language = lan;
            int brate = BiggerFromList(m.Get(StreamKind.Video, num, "BitRate"));
            if (brate != 0)
                s.Bitrate = (int) Math.Round(brate / 1000F);
            string stype = m.Get(StreamKind.Video, num, "ScanType");
            if (!string.IsNullOrEmpty(stype))
                s.ScanType = stype.ToLower();
            s.RefFrames = m.GetByte(StreamKind.Video, num, "Format_Settings_RefFrames");
            string fprofile = m.Get(StreamKind.Video, num, "Format_Profile");
            if (!string.IsNullOrEmpty(fprofile))
            {
                int a = fprofile.ToLower(CultureInfo.InvariantCulture).IndexOf("@", StringComparison.Ordinal);
                if (a > 0)
                {
                    s.Profile = TranslateProfile(s.Codec,
                        fprofile.ToLower(CultureInfo.InvariantCulture).Substring(0, a));
                    if (int.TryParse(TranslateLevel(fprofile.ToLower(CultureInfo.InvariantCulture).Substring(a + 1)),
                        out int level)) s.Level = level;
                }
                else
                    s.Profile = TranslateProfile(s.Codec, fprofile.ToLower(CultureInfo.InvariantCulture));
            }

            float rot = m.GetFloat(StreamKind.Video, num, "Rotation");

            if (rot != 0)
            {
                switch (rot)
                {
                    case 90F:
                        s.Orientation = 9;
                        break;
                    case 180F:
                        s.Orientation = 3;
                        break;
                    case 270F:
                        s.Orientation = 6;
                        break;
                }
            }

            string muxing = m.Get(StreamKind.Video, num, "MuxingMode");
            if (!string.IsNullOrEmpty(muxing))
            {
                if (muxing.ToLower(CultureInfo.InvariantCulture).Contains("strip"))
                    s.HeaderStripping = 1;
            }

            string cabac = m.Get(StreamKind.Video, num, "Format_Settings_CABAC");
            if (!string.IsNullOrEmpty(cabac))
            {
                s.Cabac = (byte) (cabac.ToLower(CultureInfo.InvariantCulture) == "yes" ? 1 : 0);
            }

            if (s.Codec == "h264")
            {
                if (s.Level == 31 && s.Cabac == 0)
                    s.HasScalingMatrix = 1;
                else
                    s.HasScalingMatrix = 0;
            }

            string fratemode = m.Get(StreamKind.Video, num, "FrameRate_Mode");
            if (!string.IsNullOrEmpty(fratemode))
                s.FrameRateMode = fratemode.ToLower(CultureInfo.InvariantCulture);
            float frate = m.GetFloat(StreamKind.Video, num, "FrameRate");
            if (frate == 0.0F)
                frate = m.GetFloat(StreamKind.Video, num, "FrameRate_Original");
            if (frate != 0.0F)
                s.FrameRate = frate;
            string colorspace = m.Get(StreamKind.Video, num, "ColorSpace");
            if (!string.IsNullOrEmpty(colorspace))
                s.ColorSpace = colorspace.ToLower(CultureInfo.InvariantCulture);
            string chromasubsampling = m.Get(StreamKind.Video, num, "ChromaSubsampling");
            if (!string.IsNullOrEmpty(chromasubsampling))
                s.ChromaSubsampling = chromasubsampling.ToLower(CultureInfo.InvariantCulture);


            byte bitdepth = m.GetByte(StreamKind.Video, num, "BitDepth");
            if (bitdepth != 0)
                s.BitDepth = bitdepth;
            string id = m.Get(StreamKind.Video, num, "ID");
            if (!string.IsNullOrEmpty(id))
            {
                if (byte.TryParse(id, out byte idx))
                {
                    s.Index = idx;
                }
            }

            string qpel = m.Get(StreamKind.Video, num, "Format_Settings_QPel");
            if (!string.IsNullOrEmpty(qpel))
            {
                s.QPel = (byte) (qpel.ToLower(CultureInfo.InvariantCulture) == "yes" ? 1 : 0);
            }

            string gmc = m.Get(StreamKind.Video, num, "Format_Settings_GMC");
            if (!string.IsNullOrEmpty(gmc))
            {
                s.GMC = gmc;
            }

            string bvop = m.Get(StreamKind.Video, num, "Format_Settings_BVOP");
            if (!string.IsNullOrEmpty(bvop) && (s.Codec != "mpeg1video"))
            {
                if (bvop == "No")
                    s.BVOP = 0;
                else if ((bvop == "1") || (bvop == "Yes"))
                    s.BVOP = 1;
            }

            string def = m.Get(StreamKind.Text, num, "Default");
            if (!string.IsNullOrEmpty(def))
            {
                if (def.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Default = 1;
            }

            string forced = m.Get(StreamKind.Text, num, "Forced");
            if (!string.IsNullOrEmpty(forced))
            {
                if (forced.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Forced = 1;
            }

            s.PA = m.GetFloat(StreamKind.Video, num, "PixelAspectRatio");
            string sp2 = m.Get(StreamKind.Video, num, "PixelAspectRatio_Original");
            if (!string.IsNullOrEmpty(sp2))
                s.PA = System.Convert.ToSingle(sp2, CultureInfo.InvariantCulture);
            if ((s.PA != 1.0) && s.Width != 0)
            {
                if (s.Width != 0)
                {
                    float width = s.Width;
                    width *= s.PA;
                    s.PixelAspectRatio = (int) Math.Round(width) + ":" + s.Width;
                }
            }

            return s;
        }

        private static Stream TranslateAudioStream(MediaInfoDLLInternal m, int num)
        {
            Stream s = new Stream
            {
                Id = m.GetInt(StreamKind.Audio, num, "UniqueID"),
                CodecID = m.Get(StreamKind.Audio, num, "CodecID"),
                Codec = TranslateCodec(m.Get(StreamKind.Audio, num, "Codec"))
            };
            string title = m.Get(StreamKind.Audio, num, "Title");
            if (!string.IsNullOrEmpty(title))
                s.Title = title;

            s.StreamType = 2;

            string lang = m.Get(StreamKind.Audio, num, "Language/String3");
            if (!string.IsNullOrEmpty(lang))
                s.LanguageCode = PostTranslateCode3(lang);

            string lan = PostTranslateLan(GetLanguageFromCode3(lang, m.Get(StreamKind.Audio, num, "Language/String1")));
            if (!string.IsNullOrEmpty(lan))
                s.Language = lan;

            int duration = m.GetInt(StreamKind.Audio, num, "Duration");
            if (duration != 0)
                s.Duration = duration;

            int brate = BiggerFromList(m.Get(StreamKind.Audio, num, "BitRate"));
            if (brate != 0)
                s.Bitrate = (int) Math.Round(brate / 1000F);

            byte bitdepth = m.GetByte(StreamKind.Audio, num, "BitDepth");
            if (bitdepth != 0)
                s.BitDepth = bitdepth;

            string fprofile = m.Get(StreamKind.Audio, num, "Format_Profile");
            if (!string.IsNullOrEmpty(fprofile))
            {
                if ((fprofile.ToLower() != "layer 3") && (fprofile.ToLower() != "dolby digital") &&
                    (fprofile.ToLower() != "pro") &&
                    (fprofile.ToLower() != "layer 2"))
                    s.Profile = fprofile.ToLower(CultureInfo.InvariantCulture);
                if (fprofile.ToLower().StartsWith("ma"))
                    s.Profile = "ma";
            }

            string fset = m.Get(StreamKind.Audio, num, "Format_Settings");
            if (!string.IsNullOrEmpty(fset) && (fset == "Little / Signed") && (s.Codec == "pcm") && (bitdepth == 16))
            {
                s.Profile = "pcm_s16le";
            }
            else if (!string.IsNullOrEmpty(fset) && (fset == "Big / Signed") && (s.Codec == "pcm") && (bitdepth == 16))
            {
                s.Profile = "pcm_s16be";
            }
            else if (!string.IsNullOrEmpty(fset) && (fset == "Little / Unsigned") && (s.Codec == "pcm") &&
                     (bitdepth == 8))
            {
                s.Profile = "pcm_u8";
            }

            string id = m.Get(StreamKind.Audio, num, "ID");
            if (!string.IsNullOrEmpty(id) && byte.TryParse(id, out byte idx)) s.Index = idx;

            int pa = BiggerFromList(m.Get(StreamKind.Audio, num, "SamplingRate"));
            if (pa != 0)
                s.SamplingRate = pa;
            int channels = BiggerFromList(m.Get(StreamKind.Audio, num, "Channel(s)"));
            if (channels != 0)
                s.Channels = (byte) channels;
            int channelso = BiggerFromList(m.Get(StreamKind.Audio, num, "Channel(s)_Original"));
            if (channelso != 0)
                s.Channels = (byte) channelso;

            string bitRateMode = m.Get(StreamKind.Audio, num, "BitRate_Mode");
            if (!string.IsNullOrEmpty(bitRateMode))
                s.BitrateMode = bitRateMode.ToLower(CultureInfo.InvariantCulture);
            string dialnorm = m.Get(StreamKind.Audio, num, "dialnorm");
            if (!string.IsNullOrEmpty(dialnorm))
                s.DialogNorm = dialnorm;
            dialnorm = m.Get(StreamKind.Audio, num, "dialnorm_Average");
            if (!string.IsNullOrEmpty(dialnorm))
                s.DialogNorm = dialnorm;

            string def = m.Get(StreamKind.Text, num, "Default");
            if (!string.IsNullOrEmpty(def))
            {
                if (def.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Default = 1;
            }

            string forced = m.Get(StreamKind.Text, num, "Forced");
            if (!string.IsNullOrEmpty(forced))
            {
                if (forced.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Forced = 1;
            }

            return s;
        }

        private static Stream TranslateTextStream(MediaInfoDLLInternal m, int num)
        {
            Stream s = new Stream
            {
                Id = m.GetInt(StreamKind.Text, num, "UniqueID"),
                CodecID = m.Get(StreamKind.Text, num, "CodecID"),

                StreamType = 3
            };
            string title = m.Get(StreamKind.Text, num, "Title");
            if (!string.IsNullOrEmpty(title))
                s.Title = title;
            else if (!string.IsNullOrEmpty(title = m.Get(StreamKind.Text, num, "Subtitle")))
                s.Title = title;

            string lang = m.Get(StreamKind.Text, num, "Language/String3");
            if (!string.IsNullOrEmpty(lang))
                s.LanguageCode = PostTranslateCode3(lang);
            string lan = PostTranslateLan(GetLanguageFromCode3(lang, m.Get(StreamKind.Text, num, "Language/String1")));
            if (!string.IsNullOrEmpty(lan))
                s.Language = lan;
            s.Index = m.GetByte(StreamKind.Text, num, "ID");

            s.Format =
                s.Codec = GetFormat(m.Get(StreamKind.Text, num, "CodecID"), m.Get(StreamKind.Text, num, "Format"));

            string def = m.Get(StreamKind.Text, num, "Default");
            if (!string.IsNullOrEmpty(def))
            {
                if (def.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Default = 1;
            }

            string forced = m.Get(StreamKind.Text, num, "Forced");
            if (!string.IsNullOrEmpty(forced))
            {
                if (forced.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Forced = 1;
            }


            return s;
        }

        private static int GetInt(this MediaInfoDLLInternal mi, StreamKind kind, int number, string par)
        {
            string dta = mi.Get(kind, number, par);
            if (int.TryParse(dta, out int val))
                return val;
            return 0;
        }

        private static byte GetByte(this MediaInfoDLLInternal mi, StreamKind kind, int number, string par)
        {
            string dta = mi.Get(kind, number, par);
            if (byte.TryParse(dta, out byte val))
                return val;
            return 0;
        }

        private static long GetLong(this MediaInfoDLLInternal mi, StreamKind kind, int number, string par)
        {
            string dta = mi.Get(kind, number, par);
            if (long.TryParse(dta, out long val))
                return val;
            return 0;
        }

        private static float GetFloat(this MediaInfoDLLInternal mi, StreamKind kind, int number, string par)
        {
            string dta = mi.Get(kind, number, par);
            if (float.TryParse(dta, out float val))
                return val;
            return 0.0F;
        }

        private static readonly object Lock = new object();

        private static MediaInfoDLLInternal _mInstance = new MediaInfoDLLInternal();


        private static void CloseMediaInfo()
        {
            _mInstance?.Dispose();
            _mInstance = null;
        }

        [HandleProcessCorruptedStateExceptions]
        private static Media GetMediaInfo(string filename)
        {
            lock (Lock)
            {
                if (_mInstance == null) _mInstance = new MediaInfoDLLInternal();
                if (_mInstance.Open(filename) == 0) return null; //it's a boolean response.
                Media m = new Media();
                Part p = new Part();
                Stream videoStream = null;

                m.Duration = p.Duration = _mInstance.GetLong(StreamKind.General, 0, "Duration");
                p.Size = _mInstance.GetLong(StreamKind.General, 0, "FileSize");

                int brate = _mInstance.GetInt(StreamKind.General, 0, "BitRate");
                if (brate != 0) m.Bitrate = (int) Math.Round(brate / 1000F);

                int chaptercount = _mInstance.GetInt(StreamKind.General, 0, "MenuCount");
                m.Chaptered = chaptercount > 0;

                int videoCount = _mInstance.GetInt(StreamKind.General, 0, "VideoCount");
                int audioCount = _mInstance.GetInt(StreamKind.General, 0, "AudioCount");
                int textCount = _mInstance.GetInt(StreamKind.General, 0, "TextCount");

                m.Container = p.Container = TranslateContainer(_mInstance.Get(StreamKind.General, 0, "Format"));
                string codid = _mInstance.Get(StreamKind.General, 0, "CodecID");
                if (!string.IsNullOrEmpty(codid) && (codid.Trim().ToLower() == "qt")) m.Container = p.Container = "mov";

                List<Stream> streams = new List<Stream>();
                byte iidx = 0;
                if (videoCount > 0)
                {
                    for (int x = 0; x < videoCount; x++)
                    {
                        try
                        {
                            Stream s;
                            s = TranslateVideoStream(_mInstance, x);

                            if (x == 0)
                            {
                                videoStream = s;
                                m.Width = s.Width;
                                m.Height = s.Height;
                                if (m.Height != 0)
                                {
                                    if (m.Width != 0)
                                    {
                                        m.VideoResolution = GetResolution(m.Width, m.Height);
                                        m.AspectRatio = GetAspectRatio(m.Width, m.Height, s.PA);
                                    }
                                }

                                if (s.FrameRate != 0)
                                {
                                    float fr = System.Convert.ToSingle(s.FrameRate, CultureInfo.InvariantCulture);
                                    string frs = ((int) Math.Round(fr)).ToString(CultureInfo.InvariantCulture);
                                    if (!string.IsNullOrEmpty(s.ScanType))
                                    {
                                        if (s.ScanType.ToLower().Contains("int"))
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

                                    m.VideoFrameRate = frs;
                                }

                                m.VideoCodec = string.IsNullOrEmpty(s.CodecID) ? s.Codec : s.CodecID;
                                if (m.Duration != 0 && s.Duration != 0)
                                {
                                    if (s.Duration > m.Duration) m.Duration = p.Duration = s.Duration;
                                }

                                if (videoCount == 1)
                                {
                                    s.Default = 0;
                                    s.Forced = 0;
                                }
                            }

                            if (m.Container != "mkv")
                            {
                                s.Index = iidx;
                                iidx++;
                            }

                            streams.Add(s);
                        }
                        catch (Exception e)
                        {
                            _logger.Error($"Unable to parse video stream in {filename}");
                        }
                    }
                }

                int totalSoundRate = 0;
                if (audioCount > 0)
                {
                    for (int x = 0; x < audioCount; x++)
                    {
                        try
                        {
                            Stream s = TranslateAudioStream(_mInstance, x);
                            if ((s.Codec == "adpcm") && (p.Container == "flv")) s.Codec = "adpcm_swf";
                            if (x == 0)
                            {
                                m.AudioCodec = string.IsNullOrEmpty(s.CodecID) ? s.Codec : s.CodecID;
                                m.AudioChannels = s.Channels;
                                if (m.Duration != 0 && s.Duration != 0)
                                {
                                    if (s.Duration > m.Duration) m.Duration = p.Duration = s.Duration;
                                }

                                if (audioCount == 1)
                                {
                                    s.Default = 0;
                                    s.Forced = 0;
                                }
                            }

                            if (s.Bitrate != 0)
                            {
                                totalSoundRate += s.Bitrate;
                            }

                            if (m.Container != "mkv")
                            {
                                s.Index = iidx;
                                iidx++;
                            }

                            streams.Add(s);
                        }
                        catch (Exception e)
                        {
                            _logger.Error($"Unable to parse audio stream in {filename}");
                        }
                    }
                }

                if ((videoStream != null) && videoStream.Bitrate == 0 && m.Bitrate != 0)
                {
                    videoStream.Bitrate = m.Bitrate - totalSoundRate;
                }

                if (textCount > 0)
                {
                    for (int x = 0; x < audioCount; x++)
                    {
                        try
                        {
                            Stream s = TranslateTextStream(_mInstance, x);
                            streams.Add(s);
                            if (textCount == 1)
                            {
                                s.Default = 0;
                                s.Forced = 0;
                            }

                            if (m.Container != "mkv")
                            {
                                s.Index = iidx;
                                iidx++;
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.Error($"Unable to parse subtitle stream in {filename}");
                        }
                    }
                }

                m.Parts = new List<Part> {p};
                bool over = false;
                if (m.Container == "mkv")
                {
                    byte val = byte.MaxValue;
                    foreach (Stream s in streams.OrderBy(a => a.Index).Skip(1))
                    {
                        if (s.Index <= 0)
                        {
                            over = true;
                            break;
                        }

                        s.idx = s.Index;
                        if (s.idx < val) val = s.idx;
                    }

                    if (val != 0 && !over)
                    {
                        foreach (Stream s in streams)
                        {
                            s.idx = (byte) (s.idx - val);
                            s.Index = s.idx;
                        }
                    }
                    else if (over)
                    {
                        byte xx = 0;
                        foreach (Stream s in streams)
                        {
                            s.idx = xx++;
                            s.Index = s.idx;
                        }
                    }

                    streams = streams.OrderBy(a => a.idx).ToList();
                }

                p.Streams = streams;
                return m;
            }
        }

        public static Media Convert(string filename, int timeout)
        {
            return ConvertAsync(filename, timeout).Result;
        }

        public static async Task<Media> ConvertAsync(string filename, int timeout)
        {
            if (filename == null)
                return null;
            try
            {
                Task<Media> mediaTask = Task.FromResult(GetMediaInfo(filename));
                bool finished;
                Media m = null;
                if (timeout > 0)
                {
                    var task = await Task.WhenAny(mediaTask, Task.Delay(TimeSpan.FromMinutes(timeout)));
                    finished = task == mediaTask;
                    if (finished) m = await mediaTask;
                }
                else
                {
                    m = await mediaTask;
                    finished = true;
                }

                if (!finished || m == null)
                {
                    try
                    {
                        CloseMediaInfo();
                    }
                    catch
                    {
                        /*ignored*/
                    }

                    return null;
                }

                Part p = m.Parts[0];
                if (p != null && (p.Container == "mp4" || p.Container == "mov"))
                {
                    p.Has64bitOffsets = 0;
                    p.OptimizedForStreaming = 0;
                    m.OptimizedForStreaming = 0;
                    byte[] buffer = new byte[8];
                    var fs = new FileStream(filename, FileMode.Open);
                    fs.Read(buffer, 0, 4);
                    int siz = (buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
                    fs.Seek(siz, SeekOrigin.Begin);
                    fs.Read(buffer, 0, 8);
                    if ((buffer[4] == 'f') && (buffer[5] == 'r') && (buffer[6] == 'e') && (buffer[7] == 'e'))
                    {
                        siz = (buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | (buffer[3] - 8);
                        fs.Seek(siz, SeekOrigin.Current);
                        fs.Read(buffer, 0, 8);
                    }

                    if ((buffer[4] == 'm') && (buffer[5] == 'o') && (buffer[6] == 'o') && (buffer[7] == 'v'))
                    {
                        p.OptimizedForStreaming = 1;
                        m.OptimizedForStreaming = 1;
                        siz = ((buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]) - 8;

                        buffer = new byte[siz];
                        fs.Read(buffer, 0, siz);
                        if (!FindInBuffer("trak", 0, siz, buffer, out int opos, out int oposmax)) return m;
                        if (!FindInBuffer("mdia", opos, oposmax, buffer, out opos, out oposmax)) return m;
                        if (!FindInBuffer("minf", opos, oposmax, buffer, out opos, out oposmax)) return m;
                        if (!FindInBuffer("stbl", opos, oposmax, buffer, out opos, out oposmax)) return m;
                        if (!FindInBuffer("co64", opos, oposmax, buffer, out opos, out oposmax)) return m;
                        p.Has64bitOffsets = 1;
                    }
                }

                return m;
            }
            finally
            {
                try
                {
                    _mInstance?.Close();
                    CloseMediaInfo();
                }
                catch
                {
                    // ignore
                }

                _mInstance = null;

                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
            }
        }

        private static bool FindInBuffer(string atom, int start, int max, byte[] buffer, out int pos, out int posmax)
        {
            pos = 0;
            posmax = 0;
            if (start + 8 >= max)
                return false;
            do
            {
                if ((buffer[start + 4] == atom[0]) && (buffer[start + 5] == atom[1]) &&
                    (buffer[start + 6] == atom[2]) &&
                    (buffer[start + 7] == atom[3]))
                {
                    pos = start + 8;
                    posmax =
                        ((buffer[start] << 24) | (buffer[start + 1] << 16) | (buffer[start + 2] << 8) |
                         buffer[start + 3]) +
                        start;
                    return true;
                }

                start += (buffer[start] << 24) | (buffer[start + 1] << 16) | (buffer[start + 2] << 8) |
                         buffer[start + 3];
            } while (start < max);

            return false;
        }

        public static readonly Dictionary<int, string> ResolutionArea;
        public static readonly Dictionary<int, string> ResolutionArea43;

        static MediaInfoParserInternal()
        {
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

        private static string GetResolution(int width, int height)
        {
            // not precise, but we are rounding and calculating distance anyway
            const double sixteenNine = 1.777778;
            const double fourThirds = 1.333333;
            double ratio = (double) width / height;

            if (Math.Sqrt((ratio - sixteenNine) * (ratio - sixteenNine)) <
                Math.Sqrt((ratio - fourThirds) * (ratio - fourThirds)))
            {
                long area = width * height;
                double keyDist = double.MaxValue;
                int key = 0;
                foreach (int resArea in ResolutionArea.Keys.ToList())
                {
                    double dist = Math.Sqrt((resArea - area) * (resArea - area));
                    if (!(dist < keyDist)) continue;
                    keyDist = dist;
                    key = resArea;
                }

                if (Math.Abs(keyDist - double.MaxValue) < 0.01D) return null;
                return ResolutionArea[key];
            }
            else
            {
                double area = width * height;
                double keyDist = double.MaxValue;
                int key = 0;
                foreach (int resArea in ResolutionArea43.Keys.ToList())
                {
                    double dist = Math.Sqrt((resArea - area) * (resArea - area));
                    if (dist < keyDist)
                    {
                        keyDist = dist;
                        key = resArea;
                    }
                }

                if (Math.Abs(keyDist - long.MaxValue) < 0.01D) return null;
                return ResolutionArea43[key];
            }
        }

        private static float GetAspectRatio(float width, float height, float pa)
        {
            float r = width / height * pa;
            if (r < 1.5F)
                return 1.33F;
            if (r < 1.72F)
                return 1.66F;
            if (r < 1.815F)
                return 1.78F;
            if (r < 2.025F)
                return 1.85F;
            if (r < 2.275F)
                return 2.20F;
            return 2.35F;
        }

        private static string GetFormat(string codecid, string format)
        {
            string s = codecid;
            if (!string.IsNullOrEmpty(s))
            {
                s = s.ToUpper();
                foreach (string k in SubFormats.Keys)
                {
                    if (s.Contains(k.ToUpper()))
                    {
                        return SubFormats[k];
                    }
                }
            }

            s = format;
            if (s.ToUpper() == "APPLE TEXT")
                return "ttxt";
            return null;
        }

        private static Dictionary<string, string> SubFormats = new Dictionary<string, string>
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

        private static Dictionary<string, string> CodecIDs = new Dictionary<string, string>
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

        private static Dictionary<string, string> FileContainers = new Dictionary<string, string>
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

        private static Dictionary<string, string> code3_post = new Dictionary<string, string>
        {
            {"ces", "cz"},
            {"deu", "ger"},
            {"fra", "fre"},
            {"ron", "rum"}
        };

        private static Dictionary<string, string> lan_post = new Dictionary<string, string>
        {
            {"dutch", "Nederlands"},
        };

        public static string[,] languages =
        {
            {@"abkhazian", "ab", "abk"},
            {@"afar", "aa", "aar"},
            {@"afrikaans", "af", "afr"},
            {@"akan", "ak", "aka"},
            {@"albanian", "sq", "sqi"},
            {@"amharic", "am", "amh"},
            {@"arabic", "ar", "ara"},
            {@"aragonese", "an", "arg"},
            {@"armenian", "hy", "hye"},
            {@"assamese", "as", "asm"},
            {@"avaric", "av", "ava"},
            {@"avestan", "ae", "ave"},
            {@"aymara", "ay", "aym"},
            {@"azerbaijani", "az", "aze"},
            {@"bambara", "bm", "bam"},
            {@"bashkir", "ba", "bak"},
            {@"basque", "eu", "eus"},
            {@"belarusian", "be", "bel"},
            {@"bengali", "bn", "ben"},
            {@"bihari languages", "bh", "bih"},
            {@"bislama", "bi", "bis"},
            {@"bosnian", "bs", "bos"},
            {@"breton", "br", "bre"},
            {@"bulgarian", "bg", "bul"},
            {@"burmese", "my", "mya"},
            {@"catalan", "ca", "cat"},
            {@"chamorro", "ch", "cha"},
            {@"chechen", "ce", "che"},
            {@"chinese", "zh", "chi"},
            {@"chinese", "zh", "zho"},
            {@"church slavic", "cu", "chu"},
            {@"chuvash", "cv", "chv"},
            {@"cornish", "kw", "cor"},
            {@"corsican", "co", "cos"},
            {@"cree", "cr", "cre"},
            {@"croatian", "hr", "hrv"},
            {@"czech", "cs", "ces"},
            {@"danish", "da", "dan"},
            {@"dhivehi", "dv", "div"},
            {@"dutch", "nl", "nld"},
            {@"dzongkha", "dz", "dzo"},
            {@"english", "en", "eng"},
            {@"esperanto", "eo", "epo"},
            {@"estonian, eesti keel", "et", "est"},
            {@"ewe", "ee", "ewe"},
            {@"faroese", "fo", "fao"},
            {@"fijian", "fj", "fij"},
            {@"finnish", "fi", "fin"},
            {@"french", "fr", "fra"},
            {@"fulah", "ff", "ful"},
            {@"galician", "gl", "glg"},
            {@"ganda", "lg", "lug"},
            {@"georgian", "ka", "kat"},
            {@"german", "de", "deu"},
            {@"guarani", "gn", "grn"},
            {@"gujarati", "gu", "guj"},
            {@"haitian", "ht", "hat"},
            {@"hausa", "ha", "hau"},
            {@"hebrew", "he", "heb"},
            {@"herero", "hz", "her"},
            {@"hindi", "hi", "hin"},
            {@"hiri motu", "ho", "hmo"},
            {@"hungarian", "hu", "hun"},
            {@"icelandic", "is", "ice"},
            {@"icelandic", "is", "isl"},
            {@"ido", "io", "ido"},
            {@"igbo", "ig", "ibo"},
            {@"indonesian", "id", "ind"},
            {@"interlingua", "ia", "ina"},
            {@"interlingue", "ie", "ile"},
            {@"inuktitut", "iu", "iku"},
            {@"inupiaq", "ik", "ipk"},
            {@"irish", "ga", "gle"},
            {@"italian", "it", "ita"},
            {@"japanese", "ja", "jpn"},
            {@"javanese", "jv", "jav"},
            {@"kalaallisut", "kl", "kal"},
            {@"kannada", "kn", "kan"},
            {@"kanuri", "kr", "kau"},
            {@"kashmiri", "ks", "kas"},
            {@"kazakh", "kk", "kaz"},
            {@"kentral khmer", "km", "khm"},
            {@"kikuyu", "ki", "kik"},
            {@"kinyarwanda", "rw", "kin"},
            {@"kirghiz", "ky", "kir"},
            {@"komi", "kv", "kom"},
            {@"kongo", "kg", "kon"},
            {@"korean", "ko", "kor"},
            {@"kuanyama", "kj", "kua"},
            {@"kurdish", "ku", "kur"},
            {@"lao", "lo", "lao"},
            {@"latin", "la", "lat"},
            {@"latvian", "lv", "lav"},
            {@"limburgan", "li", "lim"},
            {@"lingala", "ln", "lin"},
            {@"lithuanian", "lt", "lit"},
            {@"luba-katanga", "lu", "lub"},
            {@"luxembourgish", "lb", "ltz"},
            {@"macedonian", "mk", "mkd"},
            {@"malagasy", "mg", "mlg"},
            {@"malay", "ms", "msa"},
            {@"malayalam", "ml", "mal"},
            {@"maltese", "mt", "mlt"},
            {@"manx", "gv", "glv"},
            {@"maori", "mi", "mri"},
            {@"marathi", "mr", "mar"},
            {@"marshallese", "mh", "mah"},
            {@"modern greek", "el", "ell"},
            {@"mongolian", "mn", "mon"},
            {@"nauru", "na", "nau"},
            {@"navajo", "nv", "nav"},
            {@"ndonga", "ng", "ndo"},
            {@"nepali", "ne", "nep"},
            {@"norsk bokmål", "nb", "nob"},
            {@"north ndebele", "nd", "nde"},
            {@"northern sami", "se", "sme"},
            {@"norwegian nynorsk", "nn", "nno"},
            {@"norwegian", "no", "nor"},
            {@"nyanja", "ny", "nya"},
            {@"occitan", "oc", "oci"},
            {@"ojibwa", "oj", "oji"},
            {@"oriya", "or", "ori"},
            {@"oromo", "om", "orm"},
            {@"ossetian", "os", "oss"},
            {@"pali", "pi", "pli"},
            {@"panjabi", "pa", "pan"},
            {@"persian", "fa", "fas"},
            {@"polish", "pl", "pol"},
            {@"portuguese", "pt", "por"},
            {@"pushto", "ps", "pus"},
            {@"quechua", "qu", "que"},
            {@"romanian", "ro", "ron"},
            {@"romansh", "rm", "roh"},
            {@"rundi", "rn", "run"},
            {@"russian", "ru", "rus"},
            {@"samoan", "sm", "smo"},
            {@"sango", "sg", "sag"},
            {@"sanskrit", "sa", "san"},
            {@"sardinian", "sd", "snd"},
            {@"sardu", "sc", "srd"},
            {@"scottish gaelic", "gd", "gla"},
            {@"serbian", "sr", "srp"},
            {@"shona", "sn", "sna"},
            {@"sichuan yi", "ii", "iii"},
            {@"sinhala", "si", "sin"},
            {@"slovak", "sk", "slk"},
            {@"slovenian", "sl", "slv"},
            {@"somali", "so", "som"},
            {@"south ndebele", "nr", "nbl"},
            {@"southern sotho", "st", "sot"},
            {@"spanish", "es", "spa"},
            {@"sundanese", "su", "sun"},
            {@"swahili", "sw", "swa"},
            {@"swati", "ss", "ssw"},
            {@"swedish", "sv", "swe"},
            {@"tagalog", "tl", "tgl"},
            {@"tahitian", "ty", "tah"},
            {@"tajik‎", "tg", "tgk"},
            {@"tamil", "ta", "tam"},
            {@"tatar‎", "tt", "tat"},
            {@"telugu", "te", "tel"},
            {@"thai", "th", "tha"},
            {@"tibetan", "bo", "bod"},
            {@"tigrinya", "ti", "tir"},
            {@"tonga", "to", "ton"},
            {@"tsonga", "ts", "tso"},
            {@"tswana", "tn", "tsn"},
            {@"turkish", "tr", "tur"},
            {@"turkmen", "tk", "tuk"},
            {@"twi", "tw", "twi"},
            {@"uighur‎", "ug", "uig"},
            {@"ukrainian", "uk", "ukr"},
            {@"urdu", "ur", "urd"},
            {@"uzbek", "uz", "uzb"},
            {@"venda", "ve", "ven"},
            {@"vietnamese", "vi", "vie"},
            {@"volapük", "vo", "vol"},
            {@"walloon", "wa", "wln"},
            {@"welsh", "cy", "cym"},
            {@"western frisian", "fy", "fry"},
            {@"wolof", "wo", "wol"},
            {@"xhosa", "xh", "xho"},
            {@"yiddish", "yi", "yid"},
            {@"yoruba", "yo", "yor"},
            {@"zhuang", "za", "zha"},
            {@"zulu", "zu", "zul"},
        };
    }
}