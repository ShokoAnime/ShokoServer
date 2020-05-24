namespace Shoko.Server.API.v3
{
    public class Tag
    {
        public Tag() {}

        public Tag(string name)
        {
            Name = name;
        }

        /// <summary>
        /// The tag itself
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// What does the tag mean/what's it for
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// How relevant is it to the series
        /// </summary>
        public int Weight { get; set; }
    }
}