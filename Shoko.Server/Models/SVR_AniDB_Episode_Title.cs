using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Server.Models
{
    public class SVR_AniDB_Episode_Title : AniDB_Episode_Title
    {
        /// <summary>
        /// The local id for the row.
        /// </summary>
        /// <value></value>
        public new int AniDB_Episode_TitleID { get; set; }

        /// <summary>
        /// The AniDB anime episode id for which the title belongs to.
        /// </summary>
        /// <value></value>
        public new int AniDB_EpisodeID { get; set; }

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
                Language = value.GetEnum();
            }
        }

        /// <summary>
        /// The actual title.
        /// </summary>
        /// <value></value>
        public new string Title { get; set; }
    }
}