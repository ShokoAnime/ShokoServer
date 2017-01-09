namespace Shoko.Server.API.Model.core
{
    /// <summary>
    /// This is a class to which request should be bind to harvers parameters send to api
    /// </summary>
    public class API_Call_Parameters
    {
        /// <summary>
        /// String used in searching
        /// </summary>
        public string query { get; set; }
        
        /// <summary>
        /// Maximum number of items to return
        /// </summary>
        public int limit { get; set; }

        public int filter { get; set; }
        
        /// <summary>
        /// Disable cast in Serie result
        /// </summary>
        public int nocast { get; set; }
        
        /// <summary>
        /// Disable genres/tags in Serie result
        /// </summary>
        public int notag { get; set; }
        
        /// <summary>
        /// Identyfication number of object
        /// </summary>
        public int id { get; set; }
        
        /// <summary>
        /// Rating value used in voting
        /// </summary>
        public int score { get; set; }
        
        /// <summary>
        /// Paging offset (the number of first item to return) using with limit help to send more narrow data
        /// </summary>
        public int offset { get; set; }
        
        /// <summary>
        /// Level of recursive building objects (ex. for Serie with level=2 return will contain serie with all episodes but without rawfile in episodes)
        /// </summary>
        public int level { get; set; }
    }
}
