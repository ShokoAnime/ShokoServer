using Shoko.Server.Utilities;

namespace Shoko.Server.API.v2.Models.core
{
    /// <summary>
    /// This is a class to which request should be bind to harvers parameters send to api
    /// </summary>
    public class API_Call_Parameters
    {
        /// <summary>
        /// String used in searching or as parameter in 'soon'
        /// </summary>
        public string query { get; set; }

        /// <summary>
        /// Maximum number of items to return
        /// </summary>
        public int limit { get; set; }

        /// <summary>
        /// For tag searching, max number of tags to return. It will take limit and override if this is specified
        /// </summary>
        public int limit_tag { get; set; }

        /// <summary>
        /// the id of the filter 'this' is or resides in
        /// </summary>
        public int filter { get; set; }

        /// <summary>
        /// whether or not to search tags as well in search
        /// </summary>
        public int tags { get; set; }

        /// <summary>
        /// byte flags as defined in TagFilter
        /// </summary>
        public TagFilter.Filter tagfilter { get; set; } = 0;

        /// <summary>
        /// For searching, enable or disable fuzzy searching
        /// </summary>
        public int fuzzy { get; set; } = 1;

        /// <summary>
        /// Disable cast in Serie result
        /// </summary>
        public int nocast { get; set; }

        /// <summary>
        /// Disable genres/tags in Serie result
        /// </summary>
        public int notag { get; set; }

        /// <summary>
        /// GET/SET: Identyfication number of object
        /// </summary>
        public int id { get; set; }

        /// <summary>
        /// Rating value used in voting
        /// </summary>
        public int score { get; set; }

        /// <summary>
        /// GET: Paging offset (the number of first item to return) using with limit help to send more narrow data
        /// POST: current position of file (in seconds from 00:00:00 ex. 0:01:22 is 62)
        /// </summary>
        public long offset { get; set; }

        /// <summary>
        /// Level of recursive building objects (ex. for Serie with level=2 return will contain serie with all episodes but without rawfile in episodes)
        /// </summary>
        public int level { get; set; }

        /// <summary>
        /// If set to 1 then series will contain all known episodes (not only the one in collection)
        /// </summary>
        public int all { get; set; }

        /// <summary>
        /// passthru progres value (ex. in Trakt)
        /// </summary>
        public int progress { get; set; } = -1;

        /// <summary>
        /// status passthru (ex. in Trakt)
        /// </summary>
        public int status { get; set; } = -1;

        /// <summary>
        /// passthru ismovie mark for function to determinate if object is movie or episode (ex. Trakt)
        /// </summary>
        public int ismovie { get; set; }

        /// <summary>
        /// filename string for task like searching by it
        /// </summary>
        public string filename { get; set; } = string.Empty;

        /// <summary>
        /// hash string for task like searching by it
        /// </summary>
        public string hash { get; set; } = string.Empty;

        /// <summary>
        /// show all know pictures related to object
        /// </summary>
        public int allpics { get; set; }

        /// <summary>
        /// show only given number of pictures related to object
        /// </summary>
        public int pic { get; set; } = 1;

        /// <summary>
        /// skip some of the information with supported calls
        /// </summary>
        public int skip { get; set; }
    }
}