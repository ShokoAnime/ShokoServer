namespace Shoko.Models.Server
{
    /// <summary>
    /// Internal Model for Staff, this can be a seiyuu, composer, director, etc.
    /// Using Sawano Hiroyuki as an example
    /// </summary>
    public class AnimeStaff
    {
        /// <summary>
        /// Internal ID
        /// </summary>
        public int StaffID { get; set; }

        /// <summary>
        /// AniDB Creator ID
        /// ex. 6722
        /// </summary>
        public int AniDBID { get; set; }

        /// <summary>
        /// Main Name, romanized
        /// ex. Sawano Hiroyuki
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Alternate Name, this can be any other name, whether kanji, an alias, etc
        /// ex. 澤野弘之
        /// </summary>
        public string AlternateName { get; set; }

        /// <summary>
        /// A description, bio, etc
        /// ex. Sawano Hiroyuki was born September 12, 1980 in Tokyo, Japan. He is a composer and arranger.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The path to the image
        /// </summary>
        public string ImagePath { get; set; }
    }
}