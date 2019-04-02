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
        public int limit = 0;

        /// <summary>
        /// For tag searching, max number of tags to return. It will take limit and override if this is specified
        /// </summary>
        public int limit_tag = 0;

        /// <summary>
        /// the id of the filter 'this' is or resides in
        /// </summary>
        public int filter = 0;

        /// <summary>
        /// whether or not to search tags as well in search
        /// </summary>
        public int tags = 0;

        /// <summary>
        /// byte flags as defined in TagFilter
        /// </summary>
        public TagFilter.Filter tagfilter = 0;

        /// <summary>
        /// For searching, enable or disable fuzzy searching
        /// </summary>
        public int fuzzy = 1;

        /// <summary>
        /// Disable cast in Serie result
        /// </summary>
        public int nocast = 0;

        /// <summary>
        /// Disable genres/tags in Serie result
        /// </summary>
        public int notag = 0;

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
        public long offset = 0;

        /// <summary>
        /// Level of recursive building objects (ex. for Serie with level=2 return will contain serie with all episodes but without rawfile in episodes)
        /// </summary>
        public int level = 0;

        /// <summary>
        /// If set to 1 then series will contain all known episodes (not only the one in collection)
        /// </summary>
        public int all = 0;

        /// <summary>
        /// passthru progres value (ex. in Trakt)
        /// </summary>
        public int progress = -1;

        /// <summary>
        /// status passthru (ex. in Trakt)
        /// </summary>
        public int status = -1;

        /// <summary>
        /// passthru ismovie mark for function to determinate if object is movie or episode (ex. Trakt)
        /// </summary>
        public int ismovie = 0;

        /// <summary>
        /// filename string for task like searching by it
        /// </summary>
        public string filename = string.Empty;
        
        /// <summary>
        /// hash string for task like searching by it
        /// </summary>
        public string hash = string.Empty;

        /// <summary>
        /// show all know pictures related to object
        /// </summary>
        public int allpics = 0;

        /// <summary>
        /// show only given number of pictures related to object
        /// </summary>
        public int pic = 1;

        /// <summary>
        /// skip some of the information with supported calls
        /// </summary>
        public int skip = 0;
    }
}