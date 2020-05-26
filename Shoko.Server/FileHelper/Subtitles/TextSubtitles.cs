using System.Collections.Generic;
using System.IO;
using NutzCode.CloudFileSystem;
using Shoko.Models.MediaInfo;
using Shoko.Server.Models;
using Shoko.Server.Utilities.MediaInfoLib;

namespace Shoko.Server.FileHelper.Subtitles
{
    public class TextSubtitles : ISubtitles
    {
        public List<TextStream> GetStreams(SVR_VideoLocal_Place vplace)
        {
            // TODO Scan the folder for filename.lang.sub files
            string dirname = Path.GetDirectoryName(vplace.FullServerPath);
            string fname = Path.GetFileNameWithoutExtension(vplace.FilePath);
            if (string.IsNullOrEmpty(dirname) || string.IsNullOrEmpty(fname))
                return new List<TextStream>();
            string basename = Path.Combine(dirname, fname);
            HashSet<string> extensions = new HashSet<string>(SubtitleHelper.Extensions.Keys);
            extensions.Remove("idx");
            List<TextStream> streams = new List<TextStream>();
            foreach (string n in extensions)
            {
                string newname = $"{basename}.{n}";
                FileSystemResult<IObject> r = vplace.ImportFolder.FileSystem.Resolve(newname);
                if (r == null || !r.IsOk || !(r.Result is IFile)) continue;
                MediaContainer m = MediaInfo.GetMediaInfo(newname);
                List<TextStream> tStreams = m?.TextStreams;
                if (tStreams != null && tStreams.Count > 0)
                {
                    tStreams.ForEach(a => a.Filename = newname);
                    streams.AddRange(tStreams);
                }
            }
            return streams;
        }
    }
}