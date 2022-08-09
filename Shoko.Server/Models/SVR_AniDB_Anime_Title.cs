using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Server.Models
{
    public class SVR_AniDB_Anime_Title : AniDB_Anime_Title
    {
        /// <summary>
        /// The title type.
        /// </summary>
        /// <value></value>
        public new TitleType TitleType { get; set; }

        /// <summary>
        /// The language.
        /// </summary>
        /// <value></value>
        public new TitleLanguage Language { get; set; }

        /// <summary>
        /// The language code.
        /// </summary>
        /// <value></value>
        public string LanguageCode
        {
            get
            {
                return Language.GetString();
            }
            set
            {
                Language = value.GetTitleLanguage();
            }
        }
    }
}