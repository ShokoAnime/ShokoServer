using System.Collections.Generic;
using JMMContracts.PlexContracts;

namespace JMMFileHelper.Subtitles
{
    public interface ISubtitles
    {
        List<Stream> Process(string filename);
        List<Stream> GetStreams(string filename);
    }
}