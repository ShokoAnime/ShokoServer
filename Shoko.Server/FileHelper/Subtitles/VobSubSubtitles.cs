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
            string dirname = Path.GetDirectoryName(vplace.FullServerPath);
            string fname = Path.GetFileNameWithoutExtension(vplace.FilePath);
            if (string.IsNullOrEmpty(dirname) || string.IsNullOrEmpty(fname)) return new List<TextStream>();
            string basename = Path.Combine(dirname, fname);
            if (!File.Exists(basename + ".idx") || !File.Exists(basename + ".sub")) return new List<TextStream>();
            FileSystemResult<IObject> r = vplace.ImportFolder.FileSystem.Resolve(basename + ".sub");
            if (r == null || !r.IsOk || !(r.Result is IFile)) return new List<TextStream>();
            var path = basename + ".sub";
            MediaContainer m = MediaInfo.GetMediaInfo(path);
            if (m == null) return new List<TextStream>();
            return m.TextStreams;
        }
    }
}