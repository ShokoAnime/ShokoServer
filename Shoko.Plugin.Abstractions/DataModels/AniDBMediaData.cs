using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels
{
    public class AniDBMediaData
    {
        public string VideoCodec { get; set; }
        public IReadOnlyList<string> AudioCodecs { get; set; }
        public IReadOnlyList<TitleLanguage> AudioLanguages { get; set; }
        public IReadOnlyList<TitleLanguage> SubLanguages { get; set; }
    }
}