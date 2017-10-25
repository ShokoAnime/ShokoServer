using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Shoko.Models.Server;
using NutzCode.CloudFileSystem;
using Shoko.Server.Models;
using Stream = Shoko.Models.PlexAndKodi.Stream;
using Path = Pri.LongPath.Path;
using File = Pri.LongPath.File;

namespace Shoko.Server.FileHelper.Subtitles
{
    public class VobSubSubtitles : ISubtitles
    {
        public List<Stream> Process(SVR_VideoLocal_Place vplace)
        {
            string dirname = Path.GetDirectoryName(vplace.FullServerPath);
            string fname = Path.GetFileNameWithoutExtension(vplace.FilePath);
            if (string.IsNullOrEmpty(dirname) || string.IsNullOrEmpty(fname))
                return null;
            string basename = Path.Combine(dirname, fname);
            List<Stream> streams = new List<Stream>();
            if (File.Exists(basename + ".idx") && File.Exists(basename + ".sub"))
            {
                FileSystemResult<IObject> r = vplace.ImportFolder.FileSystem.Resolve(basename + ".sub");
                if (r != null && r.IsOk && r.Result is IFile)
                {
                    List<Stream> ss = GetStreams((IFile) r.Result);
                    if ((ss != null) && (ss.Count > 0))
                        streams.AddRange(ss);
                }
            }
            return streams;
        }

        public List<Stream> GetStreams(IFile file)
        {
            string dirname = Path.GetDirectoryName(file.FullName);
            string fname = Path.GetFileNameWithoutExtension(file.Name);
            if (string.IsNullOrEmpty(dirname) || string.IsNullOrEmpty(fname))
                return null;
            string basename = Path.Combine(dirname, fname);
            FileSystemResult<IObject> r = file.FileSystem.Resolve(basename + ".idx");
            if (r == null || !r.IsOk || r.Result is IDirectory)
                return null;
            FileSystemResult<System.IO.Stream> res = ((IFile) r.Result).OpenRead();
            if (res == null || !res.IsOk)
                return null;
            StreamReader reader = new StreamReader(res.Result);
            string bing = reader.ReadToEnd();
            if (!bing.Contains("VobSub index file"))
                return null;
            Regex ex = new Regex("\\nid: ([A-Za-z]{2})");
            MatchCollection ma = ex.Matches(bing);
            int x = 0;
            List<Stream> ss = new List<Stream>();
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
                        Stream s = new Stream
                        {
                            Format = "vobsub",
                            StreamType = "3",
                            SubIndex = x.ToString(),
                            File = basename + ".idx",
                            LanguageCode = SubtitleHelper.Iso639_1_TO_Iso639_3[language],
                            Language = SubtitleHelper.Iso639_1_TO_Languages[language]
                        };
                        ss.Add(s);
                    }
                }
                x++;
            }
            return ss;
        }
    }
}