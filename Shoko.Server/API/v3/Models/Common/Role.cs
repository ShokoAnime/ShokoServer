using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// This is for cast/staff
    /// </summary>
    public class Role
    {
        /// <summary>
        /// Most will be Japanese. Once AniList is in, it will have multiple options
        /// </summary>
        [Required]
        public string language { get; set; }
        
        /// <summary>
        /// The person who plays a character, writes the music, etc.
        /// </summary>
        [Required]
        public Person staff { get; set; }
        
        /// <summary>
        /// The character played, if applicable
        /// </summary>
        public Person character { get; set; }
        
        /// <summary>
        /// The role that the staff plays, cv, writer, director, etc
        /// </summary>
        [Required]
        public string role { get; set; }
        
        /// <summary>
        /// Extra info about the role. For example, role can be voice actor, while role_details is Main Character
        /// </summary>
        public string role_details { get; set; }

        /// <summary>
        /// A generic person object with the name, altname, description, and image
        /// </summary>
        public class Person
        {
            /// <summary>
            /// Main Name, romanized if needed
            /// ex. Sawano Hiroyuki
            /// </summary>
            [Required]
            public string name { get; set; }

            /// <summary>
            /// Alternate Name, this can be any other name, whether kanji, an alias, etc
            /// ex. 澤野弘之
            /// </summary>
            public string alternate_name { get; set; }

            /// <summary>
            /// A description, bio, etc
            /// ex. Sawano Hiroyuki was born September 12, 1980 in Tokyo, Japan. He is a composer and arranger.
            /// </summary>
            public string description { get; set; }

            /// <summary>
            /// image object, usually a profile picture of sorts
            /// </summary>
            public Image image { get; set; }
        }
    }
}