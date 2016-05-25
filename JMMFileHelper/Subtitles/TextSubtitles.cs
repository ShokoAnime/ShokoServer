﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Stream = JMMContracts.PlexAndKodi.Stream;

namespace JMMFileHelper.Subtitles
{
    public class TextSubtitles : ISubtitles
    {
        public List<Stream> Process(string filename)
        {
            string dirname = Path.GetDirectoryName(filename);
            string fname = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(dirname) || (string.IsNullOrEmpty(fname)))
                return null;
            string basename = Path.Combine(dirname, fname);
            HashSet<string> extensions=new HashSet<string>(SubtitleHelper.Extensions.Keys);
            extensions.Remove("idx");
            List<Stream> streams=new List<Stream>();
            foreach (string n in extensions)
            {
                string newname = basename + "." + n;
                if (File.Exists(newname))
                {
                    List<Stream> ss = GetStreams(newname);
                    if ((ss != null) && (ss.Count > 0))
                        streams.AddRange(ss);
                }
            }
            return streams;
        }

        public List<Stream> GetStreams(string filename)
        {
            string ext = Path.GetExtension(filename);
            if (string.IsNullOrEmpty(ext))
                return null;
            ext = ext.Replace(".", string.Empty).ToLower();
            string name = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(name))
                return null;
            Regex lm=new Regex(".+\\.([^\\.]+)$",RegexOptions.Singleline);
            MatchCollection col = lm.Matches(name);
            string language="xx";
            foreach (Match m in col)
            {
                if (m.Success)
                {
                    string val = m.Groups[1].Value.ToLower();
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
            string format=null;
            if ((ext == "txt") || (ext == "sub"))
            {
                string[] lines = File.ReadAllLines(filename);
                string firstline=null;
                foreach (string ws in lines)
                {
                    string k = ws.Trim();
                    if (!string.IsNullOrEmpty(k))
                    {
                        firstline = k;
                        break;
                    }
                }
                if (firstline != null)
                {
                    lm=new Regex("^\\{[0-9]+\\}\\{[0-9]*\\}",RegexOptions.Singleline);
                    Match m = lm.Match(firstline);
                    if (m.Success)
                        format = "microdvd";
                    else
                    {
                        lm=new Regex("^[0-9]{1,2}:[0-9]{2}:[0-9]{2}[:=,]",RegexOptions.Singleline);
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
            Stream s=new Stream();
            s.Format = format;
            s.StreamType = "3";
            s.File = filename;
            s.LanguageCode = SubtitleHelper.Iso639_1_TO_Iso639_3[language];
            s.Language = SubtitleHelper.Iso639_1_TO_Languages[language];
            List<Stream> sts=new List<Stream>();
            sts.Add(s);
            return sts;
        } 
    }
}
