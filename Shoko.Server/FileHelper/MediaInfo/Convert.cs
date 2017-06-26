using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using SeekOrigin = System.IO.SeekOrigin;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Models.PlexAndKodi;
using MediaInfoLib;
using NutzCode.CloudFileSystem;
using Stream = Shoko.Models.PlexAndKodi.Stream;
using Pri.LongPath;


namespace Shoko.Server.FileHelper.MediaInfo
{
    // ReSharper disable CompareOfFloatsByEqualityOperator

    public static class MediaConvert
    {
        private static string TranslateCodec(string codec)
        {
            codec = codec.ToLower();
            foreach (string k in codecs.Keys)
            {
                if (codec == k.ToLower())
                {
                    return codecs[k];
                }
            }
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
            profile = profile.ToLower();

            if (profile.Contains("advanced simple"))
                return "asp";
            if ((codec == "mpeg4") && (profile == "simple"))
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
            level = level.Replace(".", string.Empty).ToLower();
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
            c = c.ToLower();
            foreach (string k in code3_post.Keys)
            {
                if (c.Contains(k.ToLower()))
                {
                    return code3_post[k];
                }
            }
            return c;
        }

        public static string PostTranslateLan(string c)
        {
            foreach (string k in lan_post.Keys)
            {
                if (c.ToLower().Contains(k.ToLower()))
                {
                    return lan_post[k];
                }
            }
            return c;
        }

        private static string TranslateContainer(string container)
        {
            container = container.ToLower();
            foreach (string k in containers.Keys)
            {
                if (container.Contains(k.ToLower()))
                {
                    return containers[k];
                }
            }
            return container;
        }

        private static Stream TranslateVideoStream(MediaInfoLib.MediaInfo m, int num)
        {
            Stream s = new Stream
            {
                Id = m.Get(StreamKind.Video, num, "UniqueID"),
                Codec = TranslateCodec(m.Get(StreamKind.Video, num, "Codec")),
                CodecID = m.Get(StreamKind.Video, num, "CodecID"),
                StreamType = "1",
                Width = m.Get(StreamKind.Video, num, "Width")
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
            string duration = m.Get(StreamKind.Video, num, "Duration");
            if (!string.IsNullOrEmpty(duration))
                s.Duration = duration;
            s.Height = m.Get(StreamKind.Video, num, "Height");
            int brate = BiggerFromList(m.Get(StreamKind.Video, num, "BitRate"));
            if (brate != 0)
                s.Bitrate = Math.Round(brate / 1000F).ToString(CultureInfo.InvariantCulture);
            string stype = m.Get(StreamKind.Video, num, "ScanType");
            if (!string.IsNullOrEmpty(stype))
                s.ScanType = stype.ToLower();
            string refframes = m.Get(StreamKind.Video, num, "Format_Settings_RefFrames");
            if (!string.IsNullOrEmpty(refframes))
                s.RefFrames = refframes;
            string fprofile = m.Get(StreamKind.Video, num, "Format_Profile");
            if (!string.IsNullOrEmpty(fprofile))
            {
                int a = fprofile.ToLower(CultureInfo.InvariantCulture).IndexOf("@", StringComparison.Ordinal);
                if (a > 0)
                {
                    s.Profile = TranslateProfile(s.Codec,
                        fprofile.ToLower(CultureInfo.InvariantCulture).Substring(0, a));
                    s.Level = TranslateLevel(fprofile.ToLower(CultureInfo.InvariantCulture).Substring(a + 1));
                }
                else
                    s.Profile = TranslateProfile(s.Codec, fprofile.ToLower(CultureInfo.InvariantCulture));
            }
            string rot = m.Get(StreamKind.Video, num, "Rotation");

            if (!string.IsNullOrEmpty(rot))
            {
                if (float.TryParse(rot, out float val))
                {
                    if (val == 90F)
                        s.Orientation = "9";
                    else if (val == 180F)
                        s.Orientation = "3";
                    else if (val == 270F)
                        s.Orientation = "6";
                }
                else
                    s.Orientation = rot;
            }
            string muxing = m.Get(StreamKind.Video, num, "MuxingMode");
            if (!string.IsNullOrEmpty(muxing))
            {
                if (muxing.ToLower(CultureInfo.InvariantCulture).Contains("strip"))
                    s.HeaderStripping = "1";
            }
            string cabac = m.Get(StreamKind.Video, num, "Format_Settings_CABAC");
            if (!string.IsNullOrEmpty(cabac))
            {
                s.Cabac = cabac.ToLower(CultureInfo.InvariantCulture) == "yes" ? "1" : "0";
            }
            if (s.Codec == "h264")
            {
                if (!string.IsNullOrEmpty(s.Level) && (s.Level == "31") && (s.Cabac == null || s.Cabac == "0"))
                    s.HasScalingMatrix = "1";
                else
                    s.HasScalingMatrix = "0";
            }
            string fratemode = m.Get(StreamKind.Video, num, "FrameRate_Mode");
            if (!string.IsNullOrEmpty(fratemode))
                s.FrameRateMode = fratemode.ToLower(CultureInfo.InvariantCulture);
            float frate = m.GetFloat(StreamKind.Video, num, "FrameRate");
            if (frate == 0.0F)
                frate = m.GetFloat(StreamKind.Video, num, "FrameRate_Original");
            if (frate != 0.0F)
                s.FrameRate = frate.ToString("F3");
            string colorspace = m.Get(StreamKind.Video, num, "ColorSpace");
            if (!string.IsNullOrEmpty(colorspace))
                s.ColorSpace = colorspace.ToLower(CultureInfo.InvariantCulture);
            string chromasubsampling = m.Get(StreamKind.Video, num, "ChromaSubsampling");
            if (!string.IsNullOrEmpty(chromasubsampling))
                s.ChromaSubsampling = chromasubsampling.ToLower(CultureInfo.InvariantCulture);


            int bitdepth = m.GetInt(StreamKind.Video, num, "BitDepth");
            if (bitdepth != 0)
                s.BitDepth = bitdepth.ToString(CultureInfo.InvariantCulture);
            string id = m.Get(StreamKind.Video, num, "ID");
            if (!string.IsNullOrEmpty(id))
            {
                if (int.TryParse(id, out int idx))
                {
                    s.Index = idx.ToString(CultureInfo.InvariantCulture);
                }
            }
            string qpel = m.Get(StreamKind.Video, num, "Format_Settings_QPel");
            if (!string.IsNullOrEmpty(qpel))
            {
                s.QPel = qpel.ToLower(CultureInfo.InvariantCulture) == "yes" ? "1" : "0";
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
                    s.BVOP = "0";
                else if ((bvop == "1") || (bvop == "Yes"))
                    s.BVOP = "1";
            }
            string def = m.Get(StreamKind.Text, num, "Default");
            if (!string.IsNullOrEmpty(def))
            {
                if (def.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Default = "1";
            }
            string forced = m.Get(StreamKind.Text, num, "Forced");
            if (!string.IsNullOrEmpty(forced))
            {
                if (forced.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Forced = "1";
            }
            s.PA = m.GetFloat(StreamKind.Video, num, "PixelAspectRatio");
            string sp2 = m.Get(StreamKind.Video, num, "PixelAspectRatio_Original");
            if (!string.IsNullOrEmpty(sp2))
                s.PA = System.Convert.ToSingle(sp2);
            if ((s.PA != 1.0) && !string.IsNullOrEmpty(s.Width))
            {
                if (int.TryParse(s.Width, out int www))
                {
                    float width = www;
                    width *= s.PA;
                    s.PixelAspectRatio = ((int)Math.Round(width)).ToString(CultureInfo.InvariantCulture) + ":" +
                                         s.Width;
                }
            }

            return s;
        }

        private static Stream TranslateAudioStream(MediaInfoLib.MediaInfo m, int num)
        {
            Stream s = new Stream
            {
                Id = m.Get(StreamKind.Audio, num, "UniqueID"),
                CodecID = m.Get(StreamKind.Audio, num, "CodecID"),
                Codec = TranslateCodec(m.Get(StreamKind.Audio, num, "Codec"))
            };
            string title = m.Get(StreamKind.Audio, num, "Title");
            if (!string.IsNullOrEmpty(title))
                s.Title = title;
            s.StreamType = "2";
            string lang = m.Get(StreamKind.Audio, num, "Language/String3");
            if (!string.IsNullOrEmpty(lang))
                s.LanguageCode = PostTranslateCode3(lang);
            ;
            string lan = PostTranslateLan(GetLanguageFromCode3(lang, m.Get(StreamKind.Audio, num, "Language/String1")));
            if (!string.IsNullOrEmpty(lan))
                s.Language = lan;
            string duration = m.Get(StreamKind.Audio, num, "Duration");
            if (!string.IsNullOrEmpty(duration))
                s.Duration = duration;
            int brate = BiggerFromList(m.Get(StreamKind.Audio, num, "BitRate"));
            if (brate != 0)
                s.Bitrate = Math.Round(brate / 1000F).ToString(CultureInfo.InvariantCulture);
            int bitdepth = m.GetInt(StreamKind.Audio, num, "BitDepth");
            if (bitdepth != 0)
                s.BitDepth = bitdepth.ToString(CultureInfo.InvariantCulture);
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
            if (!string.IsNullOrEmpty(id))
            {
                if (int.TryParse(id, out int idx))
                {
                    s.Index = idx.ToString(CultureInfo.InvariantCulture);
                }
            }
            int pa = BiggerFromList(m.Get(StreamKind.Audio, num, "SamplingRate"));
            if (pa != 0)
                s.SamplingRate = pa.ToString(CultureInfo.InvariantCulture);
            int channels = BiggerFromList(m.Get(StreamKind.Audio, num, "Channel(s)"));
            if (channels != 0)
                s.Channels = channels.ToString(CultureInfo.InvariantCulture);
            int channelso = BiggerFromList(m.Get(StreamKind.Audio, num, "Channel(s)_Original"));
            if (channelso != 0)
                s.Channels = channelso.ToString(CultureInfo.InvariantCulture);

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
                    s.Default = "1";
            }
            string forced = m.Get(StreamKind.Text, num, "Forced");
            if (!string.IsNullOrEmpty(forced))
            {
                if (forced.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Forced = "1";
            }
            return s;
        }

        private static Stream TranslateTextStream(MediaInfoLib.MediaInfo m, int num)
        {
            Stream s = new Stream
            {
                Id = m.Get(StreamKind.Text, num, "UniqueID"),
                CodecID = m.Get(StreamKind.Text, num, "CodecID"),

                StreamType = "3"
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
            string id = m.Get(StreamKind.Text, num, "ID");
            if (!string.IsNullOrEmpty(id))
            {
                if (int.TryParse(id, out int idx))
                {
                    s.Index = idx.ToString(CultureInfo.InvariantCulture);
                }
            }
            s.Format =
                s.Codec = GetFormat(m.Get(StreamKind.Text, num, "CodecID"), m.Get(StreamKind.Text, num, "Format"));

            string def = m.Get(StreamKind.Text, num, "Default");
            if (!string.IsNullOrEmpty(def))
            {
                if (def.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Default = "1";
            }
            string forced = m.Get(StreamKind.Text, num, "Forced");
            if (!string.IsNullOrEmpty(forced))
            {
                if (forced.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Forced = "1";
            }


            return s;
        }

        private static int GetInt(this MediaInfoLib.MediaInfo mi, StreamKind kind, int number, string par)
        {
            string dta = mi.Get(kind, number, par);
            if (int.TryParse(dta, out int val))
                return val;
            return 0;
        }

        private static float GetFloat(this MediaInfoLib.MediaInfo mi, StreamKind kind, int number, string par)
        {
            string dta = mi.Get(kind, number, par);
            if (float.TryParse(dta, out float val))
                return val;
            return 0.0F;
        }

        public static object _lock = new object();

        private static MediaInfoLib.MediaInfo minstance = new MediaInfoLib.MediaInfo();


        private static void CloseMediaInfo()
        {
            minstance?.Dispose();
            minstance = null;
        }


        public static Media Convert(string filename, IFile file)
        {
            if (file == null)
                return null;
            try
            {
                lock (_lock)
                {
                    Media m = new Media();
                    Part p = new Part();
                    Thread mediaInfoThread = new Thread(() =>
                    {
                        if (minstance == null)
                            minstance = new MediaInfoLib.MediaInfo();
                        minstance.Open(filename);
                        Stream VideoStream = null;
                        int video_count = minstance.GetInt(StreamKind.General, 0, "VideoCount");
                        int audio_count = minstance.GetInt(StreamKind.General, 0, "AudioCount");
                        int text_count = minstance.GetInt(StreamKind.General, 0, "TextCount");
                        if (int.TryParse(minstance.Get(StreamKind.General, 0, "MenuCount"), out int chaptercount))
                            m.Chaptered = chaptercount > 0;
                        m.Duration = p.Duration = minstance.Get(StreamKind.General, 0, "Duration");
                        m.Container = p.Container = TranslateContainer(minstance.Get(StreamKind.General, 0, "Format"));
                        string codid = minstance.Get(StreamKind.General, 0, "CodecID");
                        if (!string.IsNullOrEmpty(codid) && (codid.Trim().ToLower() == "qt"))
                            m.Container = p.Container = "mov";

                        int brate = minstance.GetInt(StreamKind.General, 0, "BitRate");
                        if (brate != 0)
                            m.Bitrate = Math.Round(brate / 1000F).ToString(CultureInfo.InvariantCulture);
                        p.Size = minstance.Get(StreamKind.General, 0, "FileSize");
                        List<Stream> streams = new List<Stream>();
                        int iidx = 0;
                        if (video_count > 0)
                        {
                            for (int x = 0; x < video_count; x++)
                            {
                                Stream s = TranslateVideoStream(minstance, x);
                                if (x == 0)
                                {
                                    VideoStream = s;
                                    m.Width = s.Width;
                                    m.Height = s.Height;
                                    if (!string.IsNullOrEmpty(m.Height))
                                    {
                                        if (!string.IsNullOrEmpty(m.Width))
                                        {
                                            m.VideoResolution =
                                                GetResolution(float.Parse(m.Width), float.Parse(m.Height));
                                            m.AspectRatio =
                                                GetAspectRatio(float.Parse(m.Width), float.Parse(m.Height), s.PA);
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(s.FrameRate))
                                    {
                                        float fr = System.Convert.ToSingle(s.FrameRate);
                                        m.VideoFrameRate =
                                            ((int) Math.Round(fr)).ToString(CultureInfo.InvariantCulture);
                                        if (!string.IsNullOrEmpty(s.ScanType))
                                        {
                                            if (s.ScanType.ToLower().Contains("int"))
                                                m.VideoFrameRate += "i";
                                            else
                                                m.VideoFrameRate += "p";
                                        }
                                        else
                                            m.VideoFrameRate += "p";
                                        if ((m.VideoFrameRate == "25p") || (m.VideoFrameRate == "25i"))
                                            m.VideoFrameRate = "PAL";
                                        else if ((m.VideoFrameRate == "30p") || (m.VideoFrameRate == "30i"))
                                            m.VideoFrameRate = "NTSC";
                                    }
                                    m.VideoCodec = s.CodecID;
                                    if (!string.IsNullOrEmpty(m.Duration) && !string.IsNullOrEmpty(s.Duration))
                                    {
                                        double.TryParse(s.Duration, NumberStyles.Any, CultureInfo.InvariantCulture,
                                            out double sdur);
                                        double.TryParse(m.Duration, NumberStyles.Any, CultureInfo.InvariantCulture,
                                            out double mdur);
                                        if (sdur > mdur)
                                            m.Duration = p.Duration = ((int) sdur).ToString();
                                    }
                                    if (video_count == 1)
                                    {
                                        s.Default = null;
                                        s.Forced = null;
                                    }
                                }

                                if (m.Container != "mkv")
                                {
                                    s.Index = iidx.ToString(CultureInfo.InvariantCulture);
                                    iidx++;
                                }
                                streams.Add(s);
                            }
                        }
                        int totalsoundrate = 0;
                        if (audio_count > 0)
                        {
                            for (int x = 0; x < audio_count; x++)
                            {
                                Stream s = TranslateAudioStream(minstance, x);
                                if ((s.Codec == "adpcm") && (p.Container == "flv"))
                                    s.Codec = "adpcm_swf";
                                if (x == 0)
                                {
                                    m.AudioCodec = s.CodecID;
                                    m.AudioChannels = s.Channels;
                                    if (!string.IsNullOrEmpty(m.Duration) && !string.IsNullOrEmpty(s.Duration))
                                    {
                                        double.TryParse(s.Duration, NumberStyles.Any, CultureInfo.InvariantCulture,
                                            out double sdur);
                                        double.TryParse(m.Duration, NumberStyles.Any, CultureInfo.InvariantCulture,
                                            out double mdur);
                                        if (sdur > mdur)
                                            m.Duration = p.Duration = ((int) sdur).ToString();
                                    }
                                    if (audio_count == 1)
                                    {
                                        s.Default = null;
                                        s.Forced = null;
                                    }
                                }
                                if (!string.IsNullOrEmpty(s.Bitrate))
                                {
                                    double.TryParse(s.Bitrate, NumberStyles.Any, CultureInfo.InvariantCulture,
                                        out double birate);
                                    totalsoundrate += brate;
                                }
                                if (m.Container != "mkv")
                                {
                                    s.Index = iidx.ToString(CultureInfo.InvariantCulture);
                                    iidx++;
                                }
                                streams.Add(s);
                            }
                        }
                        if ((VideoStream != null) && string.IsNullOrEmpty(VideoStream.Bitrate) &&
                            !string.IsNullOrEmpty(m.Bitrate))
                        {
                            double.TryParse(m.Bitrate, NumberStyles.Any, CultureInfo.InvariantCulture, out double mrate);
                            VideoStream.Bitrate =
                                (((int) mrate) - totalsoundrate).ToString(CultureInfo.InvariantCulture);
                        }
                        if (text_count > 0)
                        {
                            for (int x = 0; x < audio_count; x++)
                            {
                                Stream s = TranslateTextStream(minstance, x);
                                streams.Add(s);
                                if (text_count == 1)
                                {
                                    s.Default = null;
                                    s.Forced = null;
                                }
                                if (m.Container != "mkv")
                                {
                                    s.Index = iidx.ToString(CultureInfo.InvariantCulture);
                                    iidx++;
                                }
                            }
                        }
                        m.Parts = new List<Part>();
                        m.Parts.Add(p);
                        bool over = false;
                        if (m.Container == "mkv")
                        {
                            int val = int.MaxValue;
                            foreach (Stream s in streams)
                            {
                                if (string.IsNullOrEmpty(s.Index))
                                {
                                    over = true;
                                    break;
                                }
                                s.idx = int.Parse(s.Index);
                                if (s.idx < val)
                                    val = s.idx;
                            }
                            if ((val != 0) && !over)
                            {
                                foreach (Stream s in streams)
                                {
                                    s.idx = s.idx - val;
                                    s.Index = s.idx.ToString(CultureInfo.InvariantCulture);
                                }
                            }
                            else if (over)
                            {
                                int xx = 0;
                                foreach (Stream s in streams)
                                {
                                    s.idx = xx++;
                                    s.Index = s.idx.ToString(CultureInfo.InvariantCulture);
                                }
                            }
                            streams = streams.OrderBy(a => a.idx).ToList();
                        }
                        p.Streams = streams;
                    });
                    mediaInfoThread.Start();
                    bool finished = mediaInfoThread.Join(TimeSpan.FromMinutes(5)); //TODO Move Timeout to settings
                    if (!finished)
                    {
                        try
                        {
                            mediaInfoThread.Abort();
                        }
                        catch
                        {
                            /*ignored*/
                        }
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
                    if ((p.Container == "mp4") || (p.Container == "mov"))
                    {
                        p.Has64bitOffsets = "0";
                        p.OptimizedForStreaming = "0";
                        m.OptimizedForStreaming = "0";
                        byte[] buffer = new byte[8];
                        FileSystemResult<System.IO.Stream> fsr = file.OpenRead();
                        if (fsr == null || !fsr.IsOk)
                            return null;
                        System.IO.Stream fs = fsr.Result;
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
                            p.OptimizedForStreaming = "1";
                            m.OptimizedForStreaming = "1";
                            siz = ((buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]) - 8;

                            buffer = new byte[siz];
                            fs.Read(buffer, 0, siz);
                            if (!FindInBuffer("trak", 0, siz, buffer, out int opos, out int oposmax)) return m;
                            if (!FindInBuffer("mdia", opos, oposmax, buffer, out opos, out oposmax)) return m;
                            if (!FindInBuffer("minf", opos, oposmax, buffer, out opos, out oposmax)) return m;
                            if (!FindInBuffer("stbl", opos, oposmax, buffer, out opos, out oposmax)) return m;
                            if (!FindInBuffer("co64", opos, oposmax, buffer, out opos, out oposmax)) return m;
                            p.Has64bitOffsets = "1";
                        }
                    }
                    return m;
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e);
            }
            finally
            {
                minstance?.Close();
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
                        ((buffer[start] << 24) | (buffer[start + 1] << 16) | (buffer[start + 2] << 8) | buffer[start + 3]) +
                        start;
                    return true;
                }
                start += (buffer[start] << 24) | (buffer[start + 1] << 16) | (buffer[start + 2] << 8) | buffer[start + 3];
            } while (start < max);

            return false;
        }

        private static string GetResolution(float width, float height)

        {
            float h = (float) Math.Round(width / 1.777777777777777F);
            if (height > h)
                h = height;
            if (h > 720)
                return "1080";
            if (h > 576)
                return "720";
            if (h > 480)
                return "576";
            if (h > 360)
                return "480";
            return "sd";
        }

        private static string GetAspectRatio(float width, float height, float pa)
        {
            float r = width / height * pa;
            if (r < 1.5F)
                return "1.33";
            if (r < 1.72F)
                return "1.66";
            if (r < 1.815F)
                return "1.78";
            if (r < 2.025F)
                return "1.85";
            if (r < 2.275F)
                return "2.20";
            return "2.35";
        }

        private static string GetFormat(string codecid, string format)
        {
            string s = codecid;
            if (!string.IsNullOrEmpty(s))
            {
                s = s.ToUpper();
                foreach (string k in formats.Keys)
                {
                    if (s.Contains(k.ToUpper()))
                    {
                        return formats[k];
                    }
                }
            }
            s = format;
            if (s.ToUpper() == "APPLE TEXT")
                return "ttxt";
            return null;
        }

        private static Dictionary<string, string> formats = new Dictionary<string, string>
        {
            {"S_IMAGE/BMP", "bmp"},
            {"S_TEXT/ASS", "ass"},
            {"S_ASS", "ass"},
            {"S_TEXT/SSA", "ssa"},
            {"S_SSA", "ssa"},
            {"S_TEXT/USF", "usf"},
            {"S_TEXT/UTF8", "srt"},
            {"S_USF", "usf"},
            {"S_VOBSUB", "vobsub"},
            {"S_HDMV/PGS", "pgs"},
            {"c608", "eia-608"},
            {"c708", "eia-708"},
            {"subp", "vobsub"},
        };

        private static Dictionary<string, string> codecs = new Dictionary<string, string>
        {
            {"V_MPEG4/ISO/AVC", "h264"},
            {"v_mpeg4/iso/asp", "mpeg4"},
            {"avc", "h264"},
            {"V_MPEG2", "mpeg2"},
            {"DX50", "mpeg4"},
            {"DIV3", "msmpeg4"},
            {"divx", "mpeg4"},
            {"MPA1L3", "mp3"},
            {"MPA2L3", "mp3"},
            {"A_FLAC", "flac"},
            {"A_AAC/MPEG4/LC/SBR", "aac"},
            {"A_AAC", "aac"},
            {"A_AC3", "ac3"},
            {"dts", "dca"},
            {"161", "wmav2"},
            {"162", "wmapro"},
            {"mpa1l2", "mp2"},
            {"mpa1l3", "mp2"},
            {"mpeg-1v", "mpeg1video"},
            {"mpeg-2v", "mpeg2video"},
            {"xvid", "mpeg4"},
            {"aac lc", "aac"},
            {"sorenson h263", "flv"},
            {"mp42", "msmpeg4v2"},
            {"mp43", "msmpeg4"},
            {"aac lc-sbr", "aac"},
            {"on2 vp6", "vp6f"},
            {"mpeg-4v", "mpeg4"},
            {"vc-1", "vc1"},
            {"2", "adpcm_ms"},
            {"dts-hd", "dca"},
            {"55", "mp3"},
            {"avc1", "h264"},
            {"mpa2.5l3", "mp3"},
            {"mpg4", "msmpeg4v1"},
            {"flv1", "flv"},
            {"aac lc-sbr-ps", "aac"}
        };

        private static Dictionary<string, string> containers = new Dictionary<string, string>
        {
            {"matroska", "mkv"},
            {"windows media", "asf"},
            {"mpeg-ps", "mpeg"},
            {"mpeg-4", "mp4"},
            {"flash video", "flv"},
            {"divx", "avi"},
            {"realmedia", "rm"},
            {"mpeg video", "mpeg"},
            {"cdxa/mpeg-ps", "mpeg"}
        };

        private static Dictionary<string, string> code3_post = new Dictionary<string, string>
        {
            {"fra", "fre"},
            {"deu", "ger"},
            {"ces", "cz"},
            {"ron", "rum"}
        };

        private static Dictionary<string, string> lan_post = new Dictionary<string, string>
        {
            {"Dutch", "Nederlands"},
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
            {@"assamese", "as", "asm"},
            {@"armenian", "hy", "hye"},
            {@"avaric", "av", "ava"},
            {@"avestan", "ae", "ave"},
            {@"aymara", "ay", "aym"},
            {@"azerbaijani", "az", "aze"},
            {@"bashkir", "ba", "bak"},
            {@"bambara", "bm", "bam"},
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
            {@"nyanja", "ny", "nya"},
            {@"chinese", "zh", "chi"},
            {@"chinese", "zh", "zho"},
            {@"chuvash", "cv", "chv"},
            {@"cornish", "kw", "cor"},
            {@"corsican", "co", "cos"},
            {@"cree", "cr", "cre"},
            {@"croatian", "hr", "hrv"},
            {@"czech", "cs", "ces"},
            {@"danish", "da", "dan"},
            {@"dhivehi", "dv", "div"},
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
            {@"german", "de", "deu"},
            {@"modern greek", "el", "ell"},
            {@"guarani", "gn", "grn"},
            {@"gujarati", "gu", "guj"},
            {@"haitian", "ht", "hat"},
            {@"hausa", "ha", "hau"},
            {@"hebrew", "he", "heb"},
            {@"herero", "hz", "her"},
            {@"hindi", "hi", "hin"},
            {@"hiri motu", "ho", "hmo"},
            {@"hungarian", "hu", "hun"},
            {@"interlingua", "ia", "ina"},
            {@"indonesian", "id", "ind"},
            {@"interlingue", "ie", "ile"},
            {@"irish", "ga", "gle"},
            {@"igbo", "ig", "ibo"},
            {@"sichuan yi", "ii", "iii"},
            {@"inupiaq", "ik", "ipk"},
            {@"ido", "io", "ido"},
            {@"icelandic", "is", "isl"},
            {@"icelandic", "is", "ice"},
            {@"italian", "it", "ita"},
            {@"inuktitut", "iu", "iku"},
            {@"japanese", "ja", "jpn"},
            {@"javanese", "jv", "jav"},
            {@"georgian", "ka", "kat"},
            {@"kongo", "kg", "kon"},
            {@"kikuyu", "ki", "kik"},
            {@"kuanyama", "kj", "kua"},
            {@"kazakh", "kk", "kaz"},
            {@"kalaallisut", "kl", "kal"},
            {@"kentral khmer", "km", "khm"},
            {@"kannada", "kn", "kan"},
            {@"korean", "ko", "kor"},
            {@"kanuri", "kr", "kau"},
            {@"kashmiri", "ks", "kas"},
            {@"kurdish", "ku", "kur"},
            {@"komi", "kv", "kom"},
            {@"kirghiz", "ky", "kir"},
            {@"latin", "la", "lat"},
            {@"luxembourgish", "lb", "ltz"},
            {@"ganda", "lg", "lug"},
            {@"limburgan", "li", "lim"},
            {@"lingala", "ln", "lin"},
            {@"lao", "lo", "lao"},
            {@"lithuanian", "lt", "lit"},
            {@"luba-katanga", "lu", "lub"},
            {@"latvian", "lv", "lav"},
            {@"malagasy", "mg", "mlg"},
            {@"marshallese", "mh", "mah"},
            {@"manx", "gv", "glv"},
            {@"maori", "mi", "mri"},
            {@"macedonian", "mk", "mkd"},
            {@"malayalam", "ml", "mal"},
            {@"mongolian", "mn", "mon"},
            {@"marathi", "mr", "mar"},
            {@"malay", "ms", "msa"},
            {@"maltese", "mt", "mlt"},
            {@"nauru", "na", "nau"},
            {@"Norsk bokmål", "nb", "nob"},
            {@"north ndebele", "nd", "nde"},
            {@"nepali", "ne", "nep"},
            {@"ndonga", "ng", "ndo"},
            {@"dutch", "nl", "nld"},
            {@"norwegian nynorsk", "nn", "nno"},
            {@"norwegian", "no", "nor"},
            {@"south ndebele", "nr", "nbl"},
            {@"navajo", "nv", "nav"},
            {@"occitan", "oc", "oci"},
            {@"ojibwa", "oj", "oji"},
            {@"church slavic", "cu", "chu"},
            {@"oromo", "om", "orm"},
            {@"oriya", "or", "ori"},
            {@"ossetian", "os", "oss"},
            {@"panjabi", "pa", "pan"},
            {@"pali", "pi", "pli"},
            {@"persian", "fa", "fas"},
            {@"polish", "pl", "pol"},
            {@"pushto", "ps", "pus"},
            {@"portuguese", "pt", "por"},
            {@"quechua", "qu", "que"},
            {@"romansh", "rm", "roh"},
            {@"rundi", "rn", "run"},
            {@"romanian", "ro", "ron"},
            {@"russian", "ru", "rus"},
            {@"kinyarwanda", "rw", "kin"},
            {@"sanskrit", "sa", "san"},
            {@"sardu", "sc", "srd"},
            {@"sardinian", "sd", "snd"},
            {@"northern sami", "se", "sme"},
            {@"samoan", "sm", "smo"},
            {@"sango", "sg", "sag"},
            {@"serbian", "sr", "srp"},
            {@"scottish gaelic", "gd", "gla"},
            {@"shona", "sn", "sna"},
            {@"sinhala", "si", "sin"},
            {@"slovak", "sk", "slk"},
            {@"slovenian", "sl", "slv"},
            {@"somali", "so", "som"},
            {@"southern sotho", "st", "sot"},
            {@"spanish", "es", "spa"},
            {@"sundanese", "su", "sun"},
            {@"swahili", "sw", "swa"},
            {@"swati", "ss", "ssw"},
            {@"swedish", "sv", "swe"},
            {@"tamil", "ta", "tam"},
            {@"telugu", "te", "tel"},
            {@"tajik‎", "tg", "tgk"},
            {@"thai", "th", "tha"},
            {@"tigrinya", "ti", "tir"},
            {@"tibetan", "bo", "bod"},
            {@"turkmen", "tk", "tuk"},
            {@"tagalog", "tl", "tgl"},
            {@"tswana", "tn", "tsn"},
            {@"tonga", "to", "ton"},
            {@"turkish", "tr", "tur"},
            {@"tsonga", "ts", "tso"},
            {@"tatar‎", "tt", "tat"},
            {@"twi", "tw", "twi"},
            {@"tahitian", "ty", "tah"},
            {@"uighur‎", "ug", "uig"},
            {@"ukrainian", "uk", "ukr"},
            {@"urdu", "ur", "urd"},
            {@"uzbek", "uz", "uzb"},
            {@"venda", "ve", "ven"},
            {@"vietnamese", "vi", "vie"},
            {@"volapük", "vo", "vol"},
            {@"walloon", "wa", "wln"},
            {@"welsh", "cy", "cym"},
            {@"wolof", "wo", "wol"},
            {@"western Frisian", "fy", "fry"},
            {@"xhosa", "xh", "xho"},
            {@"yiddish", "yi", "yid"},
            {@"yoruba", "yo", "yor"},
            {@"zhuang", "za", "zha"},
            {@"zulu", "zu", "zul"}
        };
    }
}