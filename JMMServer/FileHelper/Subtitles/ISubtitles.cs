using System.Collections.Generic;
using JMMContracts.PlexAndKodi;
using JMMServer.Entities;
using NutzCode.CloudFileSystem;

namespace JMMServer.FileHelper.Subtitles
{
    public interface ISubtitles
    {
        List<Stream> Process(VideoLocal_Place filename);
        List<Stream> GetStreams(IFile filename);
    }
}