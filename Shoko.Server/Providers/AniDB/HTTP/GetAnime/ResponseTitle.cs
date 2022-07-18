using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Server.Providers.AniDB.Http.GetAnime
{
    public class ResponseTitle
    {
        public int AnimeID { get; set; }
        public TitleType TitleType { get; set; }
        public TitleLanguage Language { get; set; }
        public string Title { get; set; }
    }
}
