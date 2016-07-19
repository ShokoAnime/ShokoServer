using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMContracts.PlexAndKodi;

namespace JMMContracts.API.Models
{
    /// <summary>
    /// Model that represents Title of Anime serie
    /// </summary>
    public class Title
    {
        /// <summary>
        /// Type of the title 
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// Language
        /// </summary>
        public string language { get; set; }

        /// <summary>
        /// Title value
        /// </summary>
        public string title { get; set; }

        /// <summary>
        /// Parametraless constructor for XML Serialization 
        /// </summary>
        public Title()
        {

        }

        /// <summary>
        /// Constructor create Title from AnimeTitle
        /// </summary>
        public Title(PlexAndKodi.AnimeTitle at)
        {
            type = at.Type;
            language = at.Language;
            title = at.Title;
        }

        public static explicit operator Title(AnimeTitle title_in)
        {
            Title title_out = new Title();

            title_out.language = title_in.Language;
            title_out.title = title_in.Title;
            title_out.type = title_in.Type;

            return title_out;
        }
    }
}
