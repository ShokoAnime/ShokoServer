using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Stream = JMMContracts.KodiContracts.Stream;

namespace JMMFileHelper.Subtitles
{
    public class KodiVobSubSubtitles : IKodiSubtitles
    {
        public List<Stream> Process(string filename)
        {
            string dirname = Path.GetDirectoryName(filename);
            string fname = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(dirname) || (string.IsNullOrEmpty(fname)))
                return null;
            string basename = Path.Combine(dirname, fname);
            List<Stream> streams=new List<Stream>();
            if (File.Exists(basename + ".idx") && File.Exists(basename + ".sub"))
            {
                List<Stream> ss = GetStreams(basename + ".sub");
                if ((ss != null) && (ss.Count>0))
                    streams.AddRange(ss);
            }
            return streams;
        }

        public List<Stream> GetStreams(string filename)
        {
            string dirname = Path.GetDirectoryName(filename);
            string fname = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(dirname) || (string.IsNullOrEmpty(fname)))
                return null;
            string basename = Path.Combine(dirname, fname);
            if (!File.Exists(basename + ".idx"))
                return null;
            string bing = File.ReadAllText(basename + ".idx");
            if (!bing.Contains("VobSub index file"))
                return null;
            Regex ex = new Regex("\\nid: ([A-Za-z]{2})");
            MatchCollection ma = ex.Matches(bing);
            int x = 0;
            List<Stream> ss=new List<Stream>();
            foreach (Match m in ma)
            {
                if (m.Success)
                {
                    string language = null;
                    string val = m.Groups[1].Value.ToLower();
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
                        Stream s = new Stream();
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
