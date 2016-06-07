using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Stream = JMMContracts.KodiContracts.Stream;

namespace JMMFileHelper.Subtitles
{
    public class KodiTextSubtitles : IKodiSubtitles
    {
        public List<Stream> Process(string filename)
        {
            var dirname = Path.GetDirectoryName(filename);
            var fname = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(dirname) || string.IsNullOrEmpty(fname))
                return null;
            var basename = Path.Combine(dirname, fname);
            var extensions = new HashSet<string>(SubtitleHelper.Extensions.Keys);
            extensions.Remove("idx");
            var streams = new List<Stream>();
            foreach (var n in extensions)
            {
                var newname = basename + "." + n;
                if (File.Exists(newname))
                {
                    var ss = GetStreams(newname);
                    if ((ss != null) && (ss.Count > 0))
                        streams.AddRange(ss);
                }
            }
            return streams;
        }

        public List<Stream> GetStreams(string filename)
        {
            var ext = Path.GetExtension(filename);
            if (string.IsNullOrEmpty(ext))
                return null;
            ext = ext.Replace(".", string.Empty).ToLower();
            var name = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(name))
                return null;
            var lm = new Regex(".+\\.([^\\.]+)$", RegexOptions.Singleline);
            var col = lm.Matches(name);
            var language = "xx";
            foreach (Match m in col)
            {
                if (m.Success)
                {
                    var val = m.Groups[1].Value.ToLower();
                    if (SubtitleHelper.Iso639_3_TO_Iso639_1.ContainsKey(val))
                    {
                        language = SubtitleHelper.Iso639_3_TO_Iso639_1[val];
                        break;
                    }
                    if (SubtitleHelper.Iso639_1_TO_Languages.ContainsKey(val))
                    {
                        language = val;
                        break;
                    }
                    if (SubtitleHelper.Languages_TO_ISO639_1_Lower.ContainsKey(val))
                    {
                        language = SubtitleHelper.Languages_TO_ISO639_1_Lower[val];
                        break;
                    }
                }
            }
            string format = null;
            if ((ext == "txt") || (ext == "sub"))
            {
                var lines = File.ReadAllLines(filename);
                string firstline = null;
                foreach (var ws in lines)
                {
                    var k = ws.Trim();
                    if (!string.IsNullOrEmpty(k))
                    {
                        firstline = k;
                        break;
                    }
                }
                if (firstline != null)
                {
                    lm = new Regex("^\\{[0-9]+\\}\\{[0-9]*\\}", RegexOptions.Singleline);
                    var m = lm.Match(firstline);
                    if (m.Success)
                        format = "microdvd";
                    else
                    {
                        lm = new Regex("^[0-9]{1,2}:[0-9]{2}:[0-9]{2}[:=,]", RegexOptions.Singleline);
                        m = lm.Match(firstline);
                        if (m.Success)
                            format = "txt";
                        else
                        {
                            if (firstline.Contains("[SUBTITLE]"))
                            {
                                format = "subviewer";
                            }
                        }
                    }
                }
            }
            ext = ext.Replace("ass", "ssa");
            if (format == null)
                format = ext;
            var s = new Stream();
            s.Format = format;
            s.StreamType = "3";
            s.File = filename;
            s.LanguageCode = SubtitleHelper.Iso639_1_TO_Iso639_3[language];
            s.Language = SubtitleHelper.Iso639_1_TO_Languages[language];
            var sts = new List<Stream>();
            sts.Add(s);
            return sts;
        }
    }
}