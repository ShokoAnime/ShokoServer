using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels
{
    public class AniDBMediaData
    {
        public IReadOnlyList<TitleLanguage> AudioLanguages { get; set; }
        public IReadOnlyList<TitleLanguage> SubLanguages { get; set; }
    }
}