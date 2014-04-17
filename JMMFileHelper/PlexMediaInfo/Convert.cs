using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JMMContracts.PlexContracts;
using Stream = JMMContracts.PlexContracts.Stream;


namespace PlexMediaInfo
{
    // ReSharper disable CompareOfFloatsByEqualityOperator

    public static class MediaConvert
    {
        private static string TranslateCodec(string codec)
        {
            codec = codec.ToLower();
            foreach (string k in codecs.Keys)
            {
                if (codec==k.ToLower())
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
                int k;
                if (int.TryParse(nams[x].Trim(), out k))
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
            if ((codec=="mpeg4") && (profile=="simple"))
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
                int lll ;
                int.TryParse(level.Substring(1), out lll);
                if (lll != 0)
                    level = lll.ToString(CultureInfo.InvariantCulture);
                else if (level.StartsWith("lm"))
                    level = "medium";
                else if (level.StartsWith("lh"))
                    level="high";
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
            for(int x=0;x<languages.GetUpperBound(0);x++)
            {
                if (languages[x, 2] == code3)
                    return languages[x, 0];
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
        private static Stream TranslateVideoStream(MediaInfo m, int num)
        {
            Stream s=new Stream();
            s.Id = m.Get(StreamKind.Video,num,"UniqueID");
            s.Codec = TranslateCodec(m.Get(StreamKind.Video, num, "Codec"));
            s.CodecID = (m.Get(StreamKind.Video, num, "CodecID"));
            s.StreamType = "1";
            s.Width = m.Get(StreamKind.Video, num, "Width");
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
            if (brate!=0)
                s.Bitrate = Math.Round(brate / 1000F).ToString(CultureInfo.InvariantCulture);
            string stype = m.Get(StreamKind.Video, num, "ScanType");
            if (!string.IsNullOrEmpty(stype))
                s.ScanType=stype.ToLower();
            string refframes = m.Get(StreamKind.Video, num, "Format_Settings_RefFrames");
            if (!string.IsNullOrEmpty(refframes))
                s.RefFrames = refframes;
            string fprofile = m.Get(StreamKind.Video, num, "Format_Profile");
            if (!string.IsNullOrEmpty(fprofile))
            {
                int a = fprofile.ToLower(CultureInfo.InvariantCulture).IndexOf("@", StringComparison.Ordinal);
                if (a > 0)
                {
                    s.Profile = TranslateProfile(s.Codec,fprofile.ToLower(CultureInfo.InvariantCulture).Substring(0, a));
                    s.Level = TranslateLevel(fprofile.ToLower(CultureInfo.InvariantCulture).Substring(a + 1));
                }
                else
                    s.Profile = TranslateProfile(s.Codec, fprofile.ToLower(CultureInfo.InvariantCulture));
            }
            string rot = m.Get(StreamKind.Video, num, "Rotation");
            
            if (!string.IsNullOrEmpty(rot))
            {
                float val;
                if (float.TryParse(rot, out val))
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
            if (s.Codec=="h264")
            {
                if (!string.IsNullOrEmpty(s.Level) && (s.Level=="31") && (s.Cabac==null || s.Cabac=="0"))
                    s.HasScalingMatrix = "1";
                else
                    s.HasScalingMatrix = "0";
            }
            string fratemode = m.Get(StreamKind.Video, num, "FrameRate_Mode");
            if (!string.IsNullOrEmpty(fratemode))
                s.FrameRateMode = fratemode.ToLower(CultureInfo.InvariantCulture);
            float frate = m.GetFloat(StreamKind.Video, num, "FrameRate");
            if (frate==0.0F)
                frate = m.GetFloat(StreamKind.Video, num, "FrameRate_Original");
            if (frate != 0.0F)
                s.FrameRate=frate.ToString("F3");
            string colorspace = m.Get(StreamKind.Video, num, "ColorSpace");
            if (!string.IsNullOrEmpty(colorspace))
                s.ColorSpace=colorspace.ToLower(CultureInfo.InvariantCulture);
            string chromasubsampling= m.Get(StreamKind.Video, num, "ChromaSubsampling");
            if (!string.IsNullOrEmpty(chromasubsampling))
                s.ChromaSubsampling=chromasubsampling.ToLower(CultureInfo.InvariantCulture);


            int bitdepth = m.GetInt(StreamKind.Video, num, "BitDepth");
            if (bitdepth != 0)
                s.BitDepth = bitdepth.ToString(CultureInfo.InvariantCulture);
            string id = m.Get(StreamKind.Video, num, "ID");
            if (!string.IsNullOrEmpty(id))
            {
                int idx;
                if (int.TryParse(id, out idx))
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
            if (!string.IsNullOrEmpty(bvop) && (s.Codec!="mpeg1video"))
            {
                if (bvop == "No")
                    s.BVOP = "0";
                else if ((bvop == "1") || (bvop=="Yes"))
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
            if ((s.PA != 1.0) && (!string.IsNullOrEmpty(s.Width)))
            {
                float width = int.Parse(s.Width);
                width *= s.PA;
                s.PixelAspectRatio=((int)Math.Round(width)).ToString(CultureInfo.InvariantCulture)+":"+s.Width;
            }

            return s;
        }
        private static Stream TranslateAudioStream(MediaInfo m, int num)
        {
            Stream s = new Stream();
            s.Id = m.Get(StreamKind.Audio, num, "UniqueID");
            s.CodecID = (m.Get(StreamKind.Audio, num, "CodecID"));
            s.Codec = TranslateCodec(m.Get(StreamKind.Audio, num, "Codec"));
            string title = m.Get(StreamKind.Audio, num, "Title");
            if (!string.IsNullOrEmpty(title))
                s.Title = title;
            s.StreamType = "2";
            string lang = m.Get(StreamKind.Audio, num, "Language/String3");
            if (!string.IsNullOrEmpty(lang))
                s.LanguageCode = PostTranslateCode3(lang); ;
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
                if ((fprofile.ToLower() != "layer 3") && (fprofile.ToLower() != "dolby digital") && (fprofile.ToLower() != "pro") && (fprofile.ToLower() != "layer 2"))
                    s.Profile = fprofile.ToLower(CultureInfo.InvariantCulture);
                if (fprofile.ToLower().StartsWith("ma"))
                    s.Profile = "ma";
            }
            string fset = m.Get(StreamKind.Audio, num, "Format_Settings");
            if ((!string.IsNullOrEmpty(fset)) && (fset == "Little / Signed") && (s.Codec == "pcm") && (bitdepth==16))
            {
                s.Profile = "pcm_s16le";
            }
            else if ((!string.IsNullOrEmpty(fset)) && (fset == "Big / Signed") && (s.Codec == "pcm") && (bitdepth == 16))
            {
                 s.Profile = "pcm_s16be";
            }
            else if ((!string.IsNullOrEmpty(fset)) && (fset == "Little / Unsigned") && (s.Codec == "pcm") &&
                     (bitdepth == 8))
            {
                s.Profile = "pcm_u8";
            }
            string id = m.Get(StreamKind.Audio, num, "ID");
            if (!string.IsNullOrEmpty(id))
            {
                int idx;
                if (int.TryParse(id, out idx))
                {
                    s.Index = idx.ToString(CultureInfo.InvariantCulture);
                }
            }
            int pa = BiggerFromList(m.Get(StreamKind.Audio, num, "SamplingRate"));
            if (pa!=0)
                s.SamplingRate=pa.ToString(CultureInfo.InvariantCulture);
            int channels = BiggerFromList(m.Get(StreamKind.Audio, num, "Channel(s)"));
            if (channels != 0)
                s.Channels=channels.ToString(CultureInfo.InvariantCulture);
            int channelso = BiggerFromList(m.Get(StreamKind.Audio, num, "Channel(s)_Original"));
            if ((channelso!=0))
                s.Channels = channelso.ToString(CultureInfo.InvariantCulture);
            
            string bitRateMode = m.Get(StreamKind.Audio, num, "BitRate_Mode");
            if (!string.IsNullOrEmpty(bitRateMode))
                s.BitrateMode = bitRateMode.ToLower(CultureInfo.InvariantCulture);
            string dialnorm = m.Get(StreamKind.Audio, num, "dialnorm");
            if (!string.IsNullOrEmpty(dialnorm))
                s.DialogNorm=dialnorm;
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
        private static Stream TranslateTextStream(MediaInfo m, int num)
        {
            Stream s = new Stream();


            s.Id = m.Get(StreamKind.Text, num, "UniqueID");
            s.CodecID = (m.Get(StreamKind.Text, num, "CodecID"));

            s.StreamType = "3";
            string title = m.Get(StreamKind.Text, num, "Title");
            if (!string.IsNullOrEmpty(title))
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
                int idx ;
                if (int.TryParse(id, out idx))
                {
                    s.Index = idx.ToString(CultureInfo.InvariantCulture);
                }
            }
            s.Format = s.Codec=GetFormat(m.Get(StreamKind.Text, num, "CodecID"), m.Get(StreamKind.Text, num, "Format"));

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
        private static int GetInt(this MediaInfo mi, StreamKind kind, int number, string par)
        {
            int val;
            string dta = mi.Get(kind, number, par);
            if (int.TryParse(dta, out val))
                return val;
            return 0;
        }
        private static float GetFloat(this MediaInfo mi, StreamKind kind, int number, string par)
        {
            float val;
            string dta = mi.Get(kind, number, par);
            if (float.TryParse(dta, out val))
                return val;
            return 0.0F;
        }
        public static Media Convert(string filename)
        {
            int ex = 0;
            MediaInfo mi = new MediaInfo();
            if (mi == null)
                return null;
            try
            {
                if (!File.Exists(filename))
                    return null;
            }
            catch (Exception)
            {
                return null;
            }
            try
            {
                mi.Open(filename);
                ex = 1;
                Media m = new Media();
                Part p = new Part();
                Stream VideoStream = null;
                int video_count = mi.GetInt(StreamKind.General, 0, "VideoCount");
                int audio_count = mi.GetInt(StreamKind.General, 0, "AudioCount");
                int text_count = mi.GetInt(StreamKind.General, 0, "TextCount");
                m.Duration = p.Duration = mi.Get(StreamKind.General, 0, "Duration");
                m.Container = p.Container = TranslateContainer(mi.Get(StreamKind.General, 0, "Format"));
                string codid = mi.Get(StreamKind.General, 0, "CodecID");
                if ((!string.IsNullOrEmpty(codid)) && (codid.Trim().ToLower() == "qt"))
                    m.Container = p.Container= "mov";

                int brate = mi.GetInt(StreamKind.General, 0, "BitRate");
                if (brate != 0)
                    m.Bitrate = Math.Round(brate / 1000F).ToString(CultureInfo.InvariantCulture);
                p.Size = mi.Get(StreamKind.General, 0, "FileSize");
                //m.Id = p.Id = mi.Get(StreamKind.General, 0, "UniqueID");

                ex = 2;
                List<Stream> streams = new List<Stream>();
                int iidx = 0;
                if (video_count > 0)
                {
                    for (int x = 0; x < video_count; x++)
                    {
                        Stream s = TranslateVideoStream(mi, x);
                        if (x == 0)
                        {
                            VideoStream = s;
                            m.Width = s.Width;
                            m.Height = s.Height;
                            if (!string.IsNullOrEmpty(m.Height))
                            {
                                
                                if (!string.IsNullOrEmpty(m.Width))
                                {
                                    m.VideoResolution = GetResolution(int.Parse(m.Width),int.Parse(m.Height));
                                    m.AspectRatio = GetAspectRatio(float.Parse(m.Width), float.Parse(m.Height), s.PA);

                                }
                            }
                            if (!string.IsNullOrEmpty(s.FrameRate))
                            {
                                float fr = System.Convert.ToSingle(s.FrameRate);
                                m.VideoFrameRate = ((int)Math.Round(fr)).ToString(CultureInfo.InvariantCulture);
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
                            m.VideoCodec = s.Codec;
                            if (!string.IsNullOrEmpty(m.Duration) && !string.IsNullOrEmpty(s.Duration))
                            {
                                if (int.Parse(s.Duration) > int.Parse(m.Duration))
                                    m.Duration = p.Duration = s.Duration;
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
                ex = 3;
                int totalsoundrate = 0;
                if (audio_count > 0)
                {
                    for (int x = 0; x < audio_count; x++)
                    {
                        Stream s = TranslateAudioStream(mi, x);
                        if ((s.Codec == "adpcm") && (p.Container == "flv"))
                            s.Codec = "adpcm_swf";
                        if (x == 0)
                        {
                            m.AudioCodec = s.Codec;
                            m.AudioChannels = s.Channels;
                            if (!string.IsNullOrEmpty(m.Duration) && !string.IsNullOrEmpty(s.Duration))
                            {
                                if (int.Parse(s.Duration) > int.Parse(m.Duration))
                                    m.Duration = p.Duration = s.Duration;
                            }
                            if (audio_count == 1)
                            {
                                s.Default = null;
                                s.Forced = null;
                            }
                        }
                        if (!string.IsNullOrEmpty(s.Bitrate))
                        {
                            totalsoundrate += int.Parse(s.Bitrate);
                        }
                            if (m.Container != "mkv")
                        {
                            s.Index = iidx.ToString(CultureInfo.InvariantCulture);
                            iidx++;
                        }
                        streams.Add(s);
                    }
                }
                if ((VideoStream!=null) && (string.IsNullOrEmpty(VideoStream.Bitrate) && (!string.IsNullOrEmpty(m.Bitrate))))
                {
                    VideoStream.Bitrate = (int.Parse(m.Bitrate) - totalsoundrate).ToString(CultureInfo.InvariantCulture);
                }


                ex = 4;
                if (text_count > 0)
                {
                    for (int x = 0; x < audio_count; x++)
                    {
                        Stream s = TranslateTextStream(mi, x);
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

                ex = 5;
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
                    if ((val != 0) && (!over))
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
                ex = 6;
                p.Streams = streams;
                if ((p.Container == "mp4") || (p.Container=="mov"))
                {
                    p.Has64bitOffsets = "0";
                    p.OptimizedForStreaming = "0";
                    m.OptimizedForStreaming = "0";
                    byte[] buffer = new byte[8];
                    FileStream fs = File.OpenRead(filename);
                    fs.Read(buffer, 0, 4);
                    int siz = buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3];
                    fs.Seek(siz, SeekOrigin.Begin);
                    fs.Read(buffer, 0, 8);
                    if ((buffer[4] == 'f') && (buffer[5] == 'r') && (buffer[6] == 'e') && (buffer[7] == 'e'))
                    {
                        siz = buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3]-8;
                        fs.Seek(siz, SeekOrigin.Current);
                        fs.Read(buffer, 0, 8);
                    }
                    if ((buffer[4] == 'm') && (buffer[5] == 'o') && (buffer[6] == 'o') && (buffer[7] == 'v'))
                    {
                        p.OptimizedForStreaming = "1";
                        m.OptimizedForStreaming = "1";
                        siz = (buffer[0] << 24 | buffer[1] << 16 | buffer[2] << 8 | buffer[3]) - 8;

                        buffer = new byte[siz];
                        fs.Read(buffer, 0, siz);
                        int opos ;
                        int oposmax ;
                        if (FindInBuffer("trak", 0, siz, buffer, out opos, out oposmax))
                        {
                            if (FindInBuffer("mdia", opos, oposmax, buffer, out opos, out oposmax))
                            {
                                if (FindInBuffer("minf", opos, oposmax, buffer, out opos, out oposmax))
                                {

                                    if (FindInBuffer("stbl", opos, oposmax, buffer, out opos, out oposmax))
                                    {
                                        if (FindInBuffer("co64", opos, oposmax, buffer, out opos, out oposmax))
                                        {
                                            p.Has64bitOffsets = "1";
                                        }

                                    }
                                }

                            }
                        }
                    }
                }
                ex = 7;
                return m;
            }
            catch (Exception e)
            {
                throw new Exception(ex+":"+e.Message,e);
                return null;
                
            }
            finally
            {
                mi.Close();
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
                if ((buffer[start + 4] == atom[0]) && (buffer[start + 5] == atom[1]) && (buffer[start + 6] == atom[2]) &&
                    (buffer[start + 7] == atom[3]))
                {
                    pos = start + 8;
                    posmax = (buffer[start] << 24 | buffer[start + 1] << 16 | buffer[start + 2] << 8 | buffer[start + 3]) + start;
                    return true;
                }
                start += (buffer[start] << 24 | buffer[start + 1] << 16 | buffer[start + 2] << 8 | buffer[start + 3]);
            } while (start<max);

            return false;
        }
        private static string GetResolution(int width, int height)

        {
            int h = (int)Math.Round((float) width/1.777777777777777F);
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
            float r = (width/height)*pa;
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
        private static Dictionary<string, string> formats = new Dictionary<string, string> {
            { "S_IMAGE/BMP","bmp"},
            { "S_TEXT/ASS","ass"},
            { "S_ASS","ass"},
            { "S_TEXT/SSA","ssa"},
            { "S_SSA","ssa"},
            { "S_TEXT/USF","usf"},
            { "S_TEXT/UTF8","srt"},
            { "S_USF","usf"},
            { "S_VOBSUB","vobsub"},
            { "S_HDMV/PGS","pgs"},
            { "c608","eia-608"},
            { "c708","eia-708"},
            { "subp","vobsub"},
        };
        private static Dictionary<string, string> codecs = new Dictionary<string, string> {
            { "V_MPEG4/ISO/AVC","h264"},
            {"v_mpeg4/iso/asp","mpeg4"},
            { "avc","h264"},
            { "V_MPEG2","mpeg2"},
            { "DX50","mpeg4"},
            { "DIV3","msmpeg4"},
            { "divx","mpeg4"},
            { "MPA1L3","mp3"},
            { "MPA2L3","mp3"},
            { "A_FLAC","flac"},
            { "A_AAC/MPEG4/LC/SBR","aac"},
            { "A_AAC","aac"},
            { "A_AC3","ac3"},
            { "dts", "dca"},
            { "161", "wmav2" },
            { "162", "wmapro" },
            { "mpa1l2", "mp2" },
            { "mpa1l3", "mp2" },
            { "mpeg-1v", "mpeg1video" },
            { "mpeg-2v", "mpeg2video" },
            { "xvid", "mpeg4" },
            { "aac lc", "aac" },
            { "sorenson h263", "flv" },
            {"mp42","msmpeg4v2"},
            {"mp43","msmpeg4"},
            {"aac lc-sbr","aac"},
            {"on2 vp6","vp6f"},
            {"mpeg-4v","mpeg4"},
            {"vc-1","vc1"},
            {"2","adpcm_ms"},
            {"dts-hd","dca"},
            {"55","mp3"},
            {"avc1","h264"},
            {"mpa2.5l3","mp3"},
            {"mpg4","msmpeg4v1"},
            {"flv1","flv"},
            {"aac lc-sbr-ps","aac"}
        };
        private static Dictionary<string, string> containers = new Dictionary<string, string>
        {
            {"matroska", "mkv"},
            {"windows media", "asf"},
            {"mpeg-ps", "mpeg"},
            {"mpeg-4","mp4"},
            {"flash video","flv"},
            {"divx","avi"},
            {"realmedia","rm"},
            {"mpeg video","mpeg"},
            {"cdxa/mpeg-ps","mpeg"}

        };
        private static Dictionary<string, string> code3_post = new Dictionary<string, string>
        {
            {"fra", "fre"},
            {"deu","ger"},
            {"ces","cz"},
            {"ron","rum"}
        };
        private static Dictionary<string, string> lan_post = new Dictionary<string, string>
        {
            {"Dutch", "Nederlands"},
        };
        private static string[,] languages =
        {
            {"Аҧсуа","ab","abk"},
            {"Afaraf","aa","aar"},
            {"Afrikaans","af","afr"},
            {"Akan","ak","aka"},
            {"Shqip","sq","sqi"},
            {"አማርኛ","am","amh"},
            {"العربية","ar","ara"},
            {"Aragonés","an","arg"},
            {"অসমীয়া","as","asm"},
            {"Հայերեն","hy","hye"},
            {"авар мацӀ, магӀарул мацӀ","av","ava"},
            {"avesta","ae","ave"},
            {"aymar aru","ay","aym"},
            {"azərbaycan dili","az","aze"},
            {"башҡорт теле","ba","bak"},
            {"bamanankan","bm","bam"},
            {"euskara, euskera","eu","eus"},
            {"Беларуская","be","bel"},
            {"বাংলা","bn","ben"},
            {"भोजपुरी","bh","bih"},
            {"Bislama","bi","bis"},
            {"bosanski jezik","bs","bos"},
            {"brezhoneg","br","bre"},
            {"български език","bg","bul"},
            {"ဗမာစာ","my","mya"},
            {"Català","ca","cat"},
            {"Chamoru","ch","cha"},
            {"нохчийн мотт","ce","che"},
            {"chiCheŵa, chinyanja","ny","nya"},
            {"中文","zh","chi"},
            {"中文","zh","zho"},
            {"чӑваш чӗлхи","cv","chv"},
            {"Kernewek","kw","cor"},
            {"corsu, lingua corsa","co","cos"},
            {"ᓀᐦᐃᔭᐍᐏᐣ","cr","cre"},
            {"hrvatski","hr","hrv"},
            {"Česky","cs","ces"},
            {"Dansk","da","dan"},
            {"ދިވެހި","dv","div"},
            {"རྫོང་ཁ","dz","dzo"},
            {"English","en","eng"},
            {"Esperanto","eo","epo"},
            {"eesti, eesti keel","et","est"},
            {"Eʋegbe","ee","ewe"},
            {"føroyskt","fo","fao"},
            {"vosa Vakaviti","fj","fij"},
            {"Suomi","fi","fin"},
            {"Français","fr","fra"},
            {"Fulfulde, Pulaar, Pular","ff","ful"},
            {"Galego","gl","glg"},
            {"Deutsch","de","deu"},
            {"Ελληνικά","el","ell"},
            {"Avañe'ẽ","gn","grn"},
            {"ગુજરાતી","gu","guj"},
            {"Kreyòl ayisyen","ht","hat"},
            {"Hausa, هَوُسَ\"","ha","hau"},
            {"עברית","he","heb"},
            {"Otjiherero","hz","her"},
            {"\"हिन्दी, हिंदी\"","hi","hin"},
            {"Hiri Motu","ho","hmo"},
            {"Magyar","hu","hun"},
            {"Interlingua","ia","ina"},
            {"Bahasa Indonesia","id","ind"},
            {"Interlingue","ie","ile"},
            {"Gaeilge","ga","gle"},
            {"Igbo","ig","ibo"},
            {"ꆇꉙ","ii","iii"},
            {"Iñupiaq, Iñupiatun","ik","ipk"},
            {"Ido","io","ido"},
            {"Íslenska","is","isl"},
            {"Íslenska","is","ice"},
            {"Italiano","it","ita"},
            {"ᐃᓄᒃᑎᑐᑦ","iu","iku"},
            {"日本語","ja","jpn"},
            {"basa Jawa","jv","jav"},
            {"ქართული","ka","kat"},
            {"KiKongo","kg","kon"},
            {"Gĩkũyũ","ki","kik"},
            {"Kuanyama","kj","kua"},
            {"Қазақ тілі","kk","kaz"},
            {"kalaallisut, kalaallit oqaasii","kl","kal"},
            {"ភាសាខ្មែរ","km","khm"},
            {"ಕನ್ನಡ","kn","kan"},
            {"한국어","ko","kor"},
            {"Kanuri","kr","kau"},
            {"कश्मीरी, كشميري‎","ks","kas"},
            {"Kurdî, كوردی‎","ku","kur"},
            {"коми кыв","kv","kom"},
            {"кыргыз тили","ky","kir"},
            {"latine, lingua latina","la","lat"},
            {"Lëtzebuergesch","lb","ltz"},
            {"Luganda","lg","lug"},
            {"Limburgs","li","lim"},
            {"Lingála","ln","lin"},
            {"ພາສາລາວ","lo","lao"},
            {"lietuvių kalba","lt","lit"},
            {"Luba-Katanga","lu","lub"},
            {"latviešu valoda","lv","lav"},
            {"Malagasy fiteny","mg","mlg"},
            {"Kajin M̧ajeļ","mh","mah"},
            {"Gailck","gv","glv"},
            {"te reo Māori","mi","mri"},
            {"македонски јазик","mk","mkd"},
            {"മലയാളം","ml","mal"},
            {"Монгол","mn","mon"},
            {"मराठी","mr","mar"},
            {"bahasa Melayu, بهاس ملايو‎","ms","msa"},
            {"Malti","mt","mlt"},
            {"Ekakairũ Naoero","na","nau"},
            {"Norsk bokmål","nb","nob"},
            {"isiNdebele","nd","nde"},
            {"नेपाली","ne","nep"},
            {"Owambo","ng","ndo"},
            {"Nederlands","nl","nld"},
            {"Norsk nynorsk","nn","nno"},
            {"Norsk","no","nor"},
            {"isiNdebele","nr","nbl"},
            {"Diné bizaad, Dinékʼehǰí","nv","nav"},
            {"Occitan","oc","oci"},
            {"ᐊᓂᔑᓈᐯᒧᐎᓐ","oj","oji"},
            {"ѩзыкъ словѣньскъ","cu","chu"},
            {"Afaan Oromoo","om","orm"},
            {"ଓଡ଼ିଆ","or","ori"},
            {"Ирон æвзаг","os","oss"},
            {"\"ਪੰਜਾਬੀ, پنجابی‎\"","pa","pan"},
            {"पाऴि","pi","pli"},
            {"فارسی","fa","fas"},
            {"Polski","pl","pol"},
            {"پښتو","ps","pus"},
            {"Português","pt","por"},
            {"Runa Simi, Kichwa","qu","que"},
            {"rumantsch grischun","rm","roh"},
            {"kiRundi","rn","run"},
            {"Română","ro","ron"},
            {"Русский язык","ru","rus"},
            {"Ikinyarwanda","rw","kin"},
            {"संस्कृतम्","sa","san"},
            {"sardu","sc","srd"},
            {"सिन्धी, سنڌي، سندھی‎","sd","snd"},
            {"Davvisámegiella","se","sme"},
            {"gagana fa'a Samoa","sm","smo"},
            {"yângâ tî sängö","sg","sag"},
            {"српски језик","sr","srp"},
            {"Gàidhlig","gd","gla"},
            {"chiShona","sn","sna"},
            {"සිංහල","si","sin"},
            {"slovenčina","sk","slk"},
            {"slovenščina","sl","slv"},
            {"Soomaaliga, af Soomaali","so","som"},
            {"Sesotho","st","sot"},
            {"Español","es","spa"},
            {"Basa Sunda","su","sun"},
            {"Kiswahili","sw","swa"},
            {"SiSwati","ss","ssw"},
            {"Svenska","sv","swe"},
            {"தமிழ்","ta","tam"},
            {"తెలుగు","te","tel"},
            {"тоҷикӣ, toğikī, تاجیکی‎","tg","tgk"},
            {"ไทย","th","tha"},
            {"ትግርኛ","ti","tir"},
            {"བོད་ཡིག","bo","bod"},
            {"Türkmen, Түркмен","tk","tuk"},
            {"Wikang Tagalog, ᜏᜒᜃᜅ᜔ ᜆᜄᜎᜓᜄ᜔","tl","tgl"},
            {"Setswana","tn","tsn"},
            {"faka Tonga","to","ton"},
            {"Türkçe","tr","tur"},
            {"Xitsonga","ts","tso"},
            {"татарча, tatarça, تاتارچا‎","tt","tat"},
            {"Twi","tw","twi"},
            {"Reo Mā`ohi","ty","tah"},
            {"Uyƣurqə, ئۇيغۇرچە‎","ug","uig"},
            {"Українська","uk","ukr"},
            {"اردو","ur","urd"},
            {"O'zbek, Ўзбек, أۇزبېك‎","uz","uzb"},
            {"Tshivenḓa","ve","ven"},
            {"Tiếng Việt","vi","vie"},
            {"Volapük","vo","vol"},
            {"Walon","wa","wln"},
            {"Cymraeg","cy","cym"},
            {"Wollof","wo","wol"},
            {"Frysk","fy","fry"},
            {"isiXhosa","xh","xho"},
            {"ייִדיש","yi","yid"},
            {"Yorùbá","yo","yor"},
            {"Saɯ cueŋƅ, Saw cuengh","za","zha"},
            {"isiZulu","zu","zul"}
        };

    }
}
