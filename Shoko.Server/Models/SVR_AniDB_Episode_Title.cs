using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Server.Models
{
    public class SVR_AniDB_Episode_Title : AniDB_Episode_Title
    {

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