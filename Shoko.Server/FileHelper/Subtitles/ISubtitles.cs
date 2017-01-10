using System.Collections.Generic;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using NutzCode.CloudFileSystem;
using Shoko.Server.Entities;

namespace Shoko.Server.FileHelper.Subtitles
{
    public interface ISubtitles
    {
        List<Stream> Process(SVR_VideoLocal_Place filename);
        List<Stream> GetStreams(IFile filename);
    }
}