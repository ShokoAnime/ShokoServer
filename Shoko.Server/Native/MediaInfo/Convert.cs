using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using NutzCode.CloudFileSystem;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server.Media;
using Shoko.Models.WebCache;
using Shoko.Server.Import;
using Shoko.Server.Models;
using SeekOrigin = System.IO.SeekOrigin;
using Stream = Shoko.Models.PlexAndKodi.Stream;
using Video = TMDbLib.Objects.General.Video;


namespace Shoko.Server.Native.MediaInfo
{
    // ReSharper disable CompareOfFloatsByEqualityOperator

    public static class MediaConvert
    {
        private static string TranslateCodec(string codec)
        {
            codec = codec.ToLowerInvariant();
            if (CodecIDs.ContainsKey(codec))
                return CodecIDs[codec];
            return codec;
        }

        private static double BiggerFromList(string list)
        {
            double max = 0;
            string[] nams = list.Split('/');
            for (int x = 0; x < nams.Length; x++)
            {
                if (double.TryParse(nams[x].Trim(), out double k))
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

        private static string Code3ToLanguage(string code3, string full)
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
        private static string Code2ToLanguage(string code2, string full)
        {
            for (int x = 0; x < languages.GetUpperBound(0); x++)
            {
                if (languages[x, 1] == code2)
                {
                    return languages[x, 0];
                }
            }
            return full;
        }
        private static string Code2ToCode3(string code2, string full)
        {
            for (int x = 0; x < languages.GetUpperBound(0); x++)
            {
                if (languages[x, 1] == code2)
                {
                    return languages[x, 2];
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

        public static void PopulateLanguage(Shoko.Models.MediaInfo.Track t, Stream stream)
        {
            string s = t.Language;
            if (string.IsNullOrEmpty(s))
                return;
            if (s.Length == 2)
            {
                stream.Language = PostTranslateLan(Code2ToLanguage(s,s));
                stream.LanguageCode = PostTranslateCode3(Code2ToCode3(s,s));
                return;
            }
            if (s.Length == 3)
            {
                stream.Language = PostTranslateLan(Code3ToLanguage(s, s));
                stream.LanguageCode = PostTranslateCode3(s);
                return;

            }
            stream.Language = s;
            stream.LanguageCode = s;
        }
        private static Stream TranslateVideoStream(Shoko.Models.MediaInfo.Track video)
        {
            Stream s = new Stream
            {
                Id = ParseInt(video.UniqueID),
                Codec = TranslateCodec(video.CodecID),
                CodecID = video.CodecID,
                StreamType = 1,
                Width = ParseInt(video.Width),
                Height = ParseInt(video.Height),
                Duration = ParseDouble(video.Duration)
            };
            string title = video.Title;
            if (!string.IsNullOrEmpty(title))
                s.Title = title;
            PopulateLanguage(video,s);
            double brate = BiggerFromList(video.BitRate);
            if (brate != 0)
                s.Bitrate = (int) Math.Round(brate / 1000F);
            string stype = video.ScanType;
            if (!string.IsNullOrEmpty(stype))
                s.ScanType = stype.ToLower();
            s.RefFrames = ParseByte(video.Format_Settings_RefFrames);
            string fprofile = video.Format_Profile;
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

            double rot = ParseDouble(video.Rotation);
            
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

            string muxing = video.MuxingMode;
            if (!string.IsNullOrEmpty(muxing))
            {
                if (muxing.ToLower(CultureInfo.InvariantCulture).Contains("strip"))
                    s.HeaderStripping = 1;
            }

            string cabac = video.Format_Settings_CABAC;
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

            string fratemode = video.FrameRate_Mode;
            if (!string.IsNullOrEmpty(fratemode))
                s.FrameRateMode = fratemode.ToLower(CultureInfo.InvariantCulture);
            double frate = ParseDouble(video.FrameRate);
            if (frate == 0.0D)
                frate = ParseDouble(video.FrameRate_Original);
            if (frate != 0.0D)
                s.FrameRate = frate;
            string colorspace = video.ColorSpace;
            if (!string.IsNullOrEmpty(colorspace))
                s.ColorSpace = colorspace.ToLower(CultureInfo.InvariantCulture);
            string chromasubsampling = video.ChromaSubsampling;
            if (!string.IsNullOrEmpty(chromasubsampling))
                s.ChromaSubsampling = chromasubsampling.ToLower(CultureInfo.InvariantCulture);


            byte bitdepth = ParseByte(video.BitDepth);
            if (bitdepth != 0)
                s.BitDepth = bitdepth;
            string id = video.ID;
            if (!string.IsNullOrEmpty(id))
            {
                if (byte.TryParse(id, out byte idx))
                {
                    s.Index = idx;
                }
            }

            string qpel = video.Format_Settings_QPel;
            if (!string.IsNullOrEmpty(qpel))
            {
                s.QPel = (byte) (qpel.ToLower(CultureInfo.InvariantCulture) == "yes" ? 1 : 0);
            }

            string gmc = video.Format_Settings_GMC;
            if (!string.IsNullOrEmpty(gmc))
            {
                s.GMC = gmc;
            }
            string bvop = video.Format_Settings_BVOP;
            if (!string.IsNullOrEmpty(bvop) && (s.Codec != "mpeg1video"))
            {
                if (bvop == "No")
                    s.BVOP = 0;
                else if ((bvop == "1") || (bvop == "Yes"))
                    s.BVOP = 1;
            }

            string def = video.Default;
            if (!string.IsNullOrEmpty(def))
            {
                if (def.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Default = 1;
            }

            string forced = video.Forced;
            if (!string.IsNullOrEmpty(forced))
            {
                if (forced.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Forced = 1;
            }

            s.PA = ParseDouble(video.PixelAspectRatio);
            string sp2 = video.PixelAspectRatio_Original;
            if (!string.IsNullOrEmpty(sp2))
                s.PA = ParseDouble(sp2);
            if ((s.PA != 1.0) && s.Width != 0)
            {
                if (s.Width != 0)
                {
                    double width = s.Width;
                    width *= s.PA;
                    s.PixelAspectRatio = (int)Math.Round(width) + ":" + s.Width;
                }
            }

            return s;
        }

        private static Stream TranslateAudioStream(Shoko.Models.MediaInfo.Track audio)
        {
            Stream s = new Stream
            {
                Id = ParseInt(audio.UniqueID),
                CodecID = audio.CodecID,
                Codec = TranslateCodec(audio.CodecID)
            };
            string title = audio.Title;
            if (!string.IsNullOrEmpty(title))
                s.Title = title;

            s.StreamType = 2;
            PopulateLanguage(audio, s);


            double duration = ParseDouble(audio.Duration);
            if (duration != 0)
                s.Duration = duration;

            double brate = BiggerFromList(audio.BitRate);
            if (brate != 0)
                s.Bitrate = (int) Math.Round(brate / 1000F);

            byte bitdepth = ParseByte(audio.BitDepth);
            if (bitdepth != 0)
                s.BitDepth = bitdepth;

            string fprofile = audio.Format_Profile;
            if (!string.IsNullOrEmpty(fprofile))
            {
                if ((fprofile.ToLower() != "layer 3") && (fprofile.ToLower() != "dolby digital") &&
                    (fprofile.ToLower() != "pro") &&
                    (fprofile.ToLower() != "layer 2"))
                    s.Profile = fprofile.ToLower(CultureInfo.InvariantCulture);
                if (fprofile.ToLower().StartsWith("ma"))
                    s.Profile = "ma";
            }
            string fset = audio.Format_Settings;
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

            string id = audio.ID;
            if (!string.IsNullOrEmpty(id) && byte.TryParse(id, out byte idx)) s.Index = idx;

            double pa = BiggerFromList(audio.SamplingRate);
            if (pa != 0)
                s.SamplingRate = (int)pa;
            double channels = BiggerFromList(audio.Channels);
            if (channels != 0)
                s.Channels = (byte) channels;
            double channelso = BiggerFromList(audio.Channels_Original);
            if (channelso != 0)
                s.Channels = (byte) channelso;

            string bitRateMode = audio.BitRate_Mode;
            if (!string.IsNullOrEmpty(bitRateMode))
                s.BitrateMode = bitRateMode.ToLower(CultureInfo.InvariantCulture);
            string dialnorm = audio.Dialnorm;
            if (!string.IsNullOrEmpty(dialnorm))
                s.DialogNorm = dialnorm;
            dialnorm = audio.Dialnorm_Average;
            if (!string.IsNullOrEmpty(dialnorm))
                s.DialogNorm = dialnorm;

            string def = audio.Default;
            if (!string.IsNullOrEmpty(def))
            {
                if (def.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Default = 1;
            }

            string forced = audio.Forced;
            if (!string.IsNullOrEmpty(forced))
            {
                if (forced.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Forced = 1;
            }
            return s;
        }

        private static Stream TranslateTextStream(Shoko.Models.MediaInfo.Track sub)
        {
            Stream s = new Stream
            {
                Id = ParseInt(sub.UniqueID),
                CodecID = sub.CodecID,

                StreamType = 3
            };
            string title = sub.Title;
            if (!string.IsNullOrEmpty(title))
                s.Title = title;
            else if (!string.IsNullOrEmpty(title = sub.Subtitle))
                s.Title = title;
            PopulateLanguage(sub, s);
            s.Index = ParseByte(sub.ID);
            s.Format = s.Codec = GetFormat(sub.CodecID, sub.Format);

            string def = sub.Default;
            if (!string.IsNullOrEmpty(def))
            {
                if (def.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Default = 1;
            }
            string forced = sub.Forced;
            if (!string.IsNullOrEmpty(forced))
            {
                if (forced.ToLower(CultureInfo.InvariantCulture) == "yes")
                    s.Forced = 1;
            }


            return s;
        }

   

        public static object _lock = new object();

        private static MediaInfo minstance = new MediaInfo();


        private static void CloseMediaInfo()
        {
            minstance?.Dispose();
            minstance = null;
        }
        private static Regex version1=new Regex("v(.*?)\\.(.*?)\\.(.*?)\\.(.*?)\\s?",RegexOptions.Compiled);
        private static Regex version2 = new Regex("v(.*?)\\.(.*?)\\.(.*?)\\s?", RegexOptions.Compiled);
        private static Regex version3 = new Regex("v(.*?)\\.(.*?)\\s?", RegexOptions.Compiled);
        private static Regex remref= new Regex("@ref.*?\\n", RegexOptions.Compiled);
        private static int ParseVersion(string version)
        {
            int val = 0;
            Match m = version1.Match(version);
            if (m.Success)
                return int.Parse(m.Groups[1].Value) * 1000000 + int.Parse(m.Groups[2].Value) * 10000 + int.Parse(m.Groups[3].Value) * 100 + int.Parse(m.Groups[4].Value);
            m = version2.Match(version);
            if (m.Success)
                return int.Parse(m.Groups[1].Value) * 1000000 + int.Parse(m.Groups[2].Value) * 10000 + int.Parse(m.Groups[3].Value) * 100;
            m = version3.Match(version);
            if (m.Success)
                return int.Parse(m.Groups[1].Value) * 1000000 + int.Parse(m.Groups[2].Value) * 10000;
            return 0;            
        }
        public static MediaStoreInfo GetMediaInfo(string filename, IFile file)
        {
            if (file == null)
                return null;
            try
            {
                lock (_lock)
                {
                    MediaStoreInfo m = null;
                    Thread mediaInfoThread = new Thread(() =>
                    {
                        if (minstance == null)
                            minstance = new MediaInfo();
                        if (minstance.Open(filename) == 0) return; //it's a boolean response.
                        minstance.Option("Inform", "JSON");
                        minstance.Option("Complete", "1");
                        minstance.Option("SkipBinaryData", "1");
                        string version = minstance.Option("Info_Version");
                        m=new MediaStoreInfo();
                        string minfo= minstance.Inform();
                        //Remove @ref
                        m.MediaInfo= remref.Replace(minfo, string.Empty);
                        m.Version = ParseVersion(version);
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

                    return m;
                }
            }
            finally
            {
                minstance?.Close();
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
            }
        }

        private static long ParseLong(string value)
        {
            long val = 0;
            if (string.IsNullOrEmpty(value))
                return val;
            long.TryParse(value, out val);
            return val;
        }
        private static int ParseInt(string value)
        {
            int val = 0;
            if (string.IsNullOrEmpty(value))
                return val;
            int.TryParse(value, out val);
            return val;
        }
        private static byte ParseByte(string value)
        {
            byte val = 0;
            if (string.IsNullOrEmpty(value))
                return val;
            byte.TryParse(value, out val);
            return val;
        }
        private static double ParseDouble(string value)
        {
            double val = 0;
            if (string.IsNullOrEmpty(value))
                return val;
            double.TryParse(value, out val);
            return val;
        }
        public static Media ConvertToPlexMedia(string mediainfo, SVR_VideoLocal vl)
        {
            Shoko.Models.MediaInfo.MediaInfo minfo = JsonConvert.DeserializeObject<Shoko.Models.MediaInfo.MediaInfo>(mediainfo);
            Media m = new Media();
            Part p = new Part();
            Shoko.Models.MediaInfo.Track general = minfo.Media.Tracks.First(a => a.Type == "General");
            m.Duration = p.Duration = ParseDouble(general.Duration);
            p.Size = ParseLong(general.FileSize);
            double brate = ParseDouble(general.BitRate);
            if (brate != 0)
                m.Bitrate = (int)Math.Round(brate / 1000F);
            int chaptercount = ParseInt(general.MenuCount);
            m.Chaptered = chaptercount > 0;
            int video_count = ParseInt(general.VideoCount);
            int audio_count = ParseInt(general.AudioCount);
            int text_count = ParseInt(general.TextCount);
            m.Container = p.Container = TranslateContainer(general.Format);
            string codid = general.CodecID;
            if (!string.IsNullOrEmpty(codid) && (codid.Trim().ToLower() == "qt"))
                m.Container = p.Container = "mov";
            List<Stream> streams = new List<Stream>();
            byte iidx = 0;
            Stream VideoStream = null;
            if (video_count > 0)
            {
                int x = 0;
                foreach (Shoko.Models.MediaInfo.Track video in minfo.Media.Tracks.Where(a => a.Type == "Video"))
                {
                    Stream s = TranslateVideoStream(video);
                    if (x == 0)
                    {
                        VideoStream = s;
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
                            float fr = System.Convert.ToSingle(s.FrameRate);
                            string frs = ((int)Math.Round(fr)).ToString(CultureInfo.InvariantCulture);
                            if (!string.IsNullOrEmpty(s.ScanType))
                            {
                                if (s.ScanType.ToLower().Contains("int"))
                                    frs += "i";
                                else
                                    frs += "p";
                            }
                            else
                                frs += "p";
                            if ((frs == "25p") || (frs == "25i"))
                                frs = "PAL";
                            else if ((frs == "30p") || (frs == "30i"))
                                frs = "NTSC";
                            m.VideoFrameRate = frs;
                        }
                        m.VideoCodec = string.IsNullOrEmpty(s.CodecID) ? s.Codec : s.CodecID;
                        if (m.Duration != 0 && s.Duration != 0)
                        {
                            if (s.Duration > m.Duration)
                                m.Duration = p.Duration = s.Duration;
                        }
                        if (video_count == 1)
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
                    x++;
                }
            }
            int totalsoundrate = 0;
            if (audio_count > 0)
            {
                int x = 0;
                foreach (Shoko.Models.MediaInfo.Track audio in minfo.Media.Tracks.Where(a => a.Type == "Audio"))
                {
                    Stream s = TranslateAudioStream(audio);
                    if ((s.Codec == "adpcm") && (p.Container == "flv"))
                        s.Codec = "adpcm_swf";
                    if (x == 0)
                    {
                        m.AudioCodec = string.IsNullOrEmpty(s.CodecID) ? s.Codec : s.CodecID;
                        m.AudioChannels = s.Channels;
                        if (m.Duration != 0 && s.Duration != 0)
                        {
                            if (s.Duration > m.Duration)
                                m.Duration = p.Duration = s.Duration;
                        }
                        if (audio_count == 1)
                        {
                            s.Default = 0;
                            s.Forced = 0;
                        }
                    }
                    if (s.Bitrate != 0)
                    {
                        totalsoundrate += s.Bitrate;
                    }
                    if (m.Container != "mkv")
                    {
                        s.Index = iidx;
                        iidx++;
                    }
                    streams.Add(s);
                    x++;
                }
            }
            if ((VideoStream != null) && VideoStream.Bitrate == 0 && m.Bitrate != 0)
            {
                VideoStream.Bitrate = m.Bitrate - totalsoundrate;
            }
            if (text_count > 0)
            {
                foreach (Shoko.Models.MediaInfo.Track sub in minfo.Media.Tracks.Where(a => a.Type == "Text"))

                {
                    Stream s = TranslateTextStream(sub);
                    streams.Add(s);
                    if (text_count == 1)
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
            }

            m.Parts = new List<Part> { p };
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
                    if (s.idx < val)
                        val = s.idx;
                }
                if (val != 0 && !over)
                {
                    foreach (Stream s in streams)
                    {
                        s.idx = (byte)(s.idx - val);
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
            m.Id = vl.VideoLocalID;
            if (string.IsNullOrEmpty(vl.SubtitleStreams))
                return m;
            List<Stream> subs = JsonConvert.DeserializeObject<List<Stream>>(vl.SubtitleStreams);
            if (subs!=null && subs.Count>0)
            {
                m.Parts[0].Streams.AddRange(subs);
            }

            foreach (Part pa in m.Parts)
            {
                pa.Id = 0;
                pa.Accessible = 1;
                pa.Exists = 1;
                bool vid = false;
                bool aud = false;
                bool txt = false;
                foreach (Stream ss in pa.Streams.ToArray())
                {
                    if (ss.StreamType == 1 && !vid) vid = true;
                    if (ss.StreamType == 2 && !aud)
                    {
                        aud = true;
                        ss.Selected = 1;
                    }

                    if (ss.StreamType == 3 && !txt)
                    {
                        txt = true;
                        ss.Selected = 1;
                    }
                }
            }
            return m;
        }

        //MP4 code Has64BitOffset and OptimizedForStreaming code in here, if we still needed in the future
        /*
                    if ((p.Container == "mp4") || (p.Container == "mov"))
                    {
                        p.Has64bitOffsets = 0;
                        p.OptimizedForStreaming = 0;
                        m.OptimizedForStreaming = 0;
                        byte[] buffer = new byte[8];
        FileSystemResult<System.IO.Stream> fsr = file.OpenRead();
                        if (fsr == null || fsr.Status != NutzCode.CloudFileSystem.Status.Ok)
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
       
         */



        private static string GetResolution(int width, int height)
        {
            return FileQualityFilter.GetResolution(Tuple.Create(width, height));
        }

        private static double GetAspectRatio(float width, float height, double pa)
        {
            double r = width / height * pa;
            if (r < 1.5D)
                return 1.33D;
            if (r < 1.72D)
                return 1.66D;
            if (r < 1.815D)
                return 1.78D;
            if (r < 2.025D)
                return 1.85D;
            if (r < 2.275D)
                return 2.20D;
            return 2.35D;
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