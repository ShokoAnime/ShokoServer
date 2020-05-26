using System.Collections.Generic;
using System.IO;
using NutzCode.CloudFileSystem;
using Shoko.Models.MediaInfo;
using Shoko.Server.Models;
using Shoko.Server.Utilities.MediaInfoLib;

namespace Shoko.Server.FileHelper.Subtitles
{
    public class VobSubSubtitles : ISubtitles
    {
        public List<TextStream> GetStreams(SVR_VideoLocal_Place vplace)
        {
            // TODO Scan the folder for filename.lang.sub files
            string dirname = Path.GetDirectoryName(vplace.FullServerPath);
            string fname = Path.GetFileNameWithoutExtension(vplace.FilePath);
            if (string.IsNullOrEmpty(dirname) || string.IsNullOrEmpty(fname)) return new List<TextStream>();
            string basename = Path.Combine(dirname, fname);
            var path = basename + ".sub";
            if (!File.Exists(basename + ".idx") || !File.Exists(path)) return new List<TextStream>();
            FileSystemResult<IObject> r = vplace.ImportFolder.FileSystem.Resolve(path);
            if (r == null || !r.IsOk || !(r.Result is IFile)) return new List<TextStream>();
            
            MediaContainer m = MediaInfo.GetMediaInfo(path);
            if (m == null) return new List<TextStream>();
            m.TextStreams.ForEach(a => a.Filename = path);
            return m.TextStreams;
        }
    }
}