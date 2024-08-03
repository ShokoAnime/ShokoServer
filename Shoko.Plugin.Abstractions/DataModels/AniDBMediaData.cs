using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels
{
    public class AniDBMediaData
    {
        public IReadOnlyList<TitleLanguage> AudioLanguages { get; set; } = new List<TitleLanguage>();
        public IReadOnlyList<TitleLanguage> SubLanguages { get; set; } = new List<TitleLanguage>();
    }
}
