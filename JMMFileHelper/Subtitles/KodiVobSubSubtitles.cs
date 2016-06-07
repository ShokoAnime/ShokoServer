using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Stream = JMMContracts.KodiContracts.Stream;

namespace JMMFileHelper.Subtitles
{
    public class KodiVobSubSubtitles : IKodiSubtitles
    {
        public List<Stream> Process(string filename)
        {
            var dirname = Path.GetDirectoryName(filename);
            var fname = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(dirname) || string.IsNullOrEmpty(fname))
                return null;
            var basename = Path.Combine(dirname, fname);
            var streams = new List<Stream>();
            if (File.Exists(basename + ".idx") && File.Exists(basename + ".sub"))
            {
                var ss = GetStreams(basename + ".sub");
                if ((ss != null) && (ss.Count > 0))
                    streams.AddRange(ss);
            }
            return streams;
        }

        public List<Stream> GetStreams(string filename)
        {
            var dirname = Path.GetDirectoryName(filename);
            var fname = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(dirname) || string.IsNullOrEmpty(fname))
                return null;
            var basename = Path.Combine(dirname, fname);
            if (!File.Exists(basename + ".idx"))
                return null;
            var bing = File.ReadAllText(basename + ".idx");
            if (!bing.Contains("VobSub index file"))
                return null;
            var ex = new Regex("\\nid: ([A-Za-z]{2})");
            var ma = ex.Matches(bing);
            var x = 0;
            var ss = new List<Stream>();
            foreach (Match m in ma)
            {
                if (m.Success)
                {
                    string language = null;
                    var val = m.Groups[1].Value.ToLower();
                    if (SubtitleHelper.Iso639_3_TO_Iso639_1.ContainsKey(val))
                    {
                        language = SubtitleHelper.Iso639_3_TO_Iso639_1[val];
                    }
                    else if (SubtitleHelper.Iso639_1_TO_Languages.ContainsKey(val))
                    {
                        language = val;
                    }
                    else if (SubtitleHelper.Languages_TO_ISO639_1_Lower.ContainsKey(val))
                    {
                        language = SubtitleHelper.Languages_TO_ISO639_1_Lower[val];
                    }
                    if (language != null)
                    {
                        var s = new Stream();
                        s.Format = "vobsub";
                        s.StreamType = "3";
                        s.SubIndex = x.ToString();
                        s.File = basename + ".idx";
                        s.LanguageCode = SubtitleHelper.Iso639_1_TO_Iso639_3[language];
                        s.Language = SubtitleHelper.Iso639_1_TO_Languages[language];
                        ss.Add(s);
                    }
                }
                x++;
            }
            return ss;
        }
    }
}