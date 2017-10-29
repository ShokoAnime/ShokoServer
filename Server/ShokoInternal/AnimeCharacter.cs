namespace Shoko.Models.Server
{
    /// <summary>
    /// Internal Model for Characters
    /// Using Shiro as an example
    /// </summary>
    public class AnimeCharacter
    {
        /// <summary>
        /// Internal ID
        /// </summary>
        public int CharacterID { get; set; }

        /// <summary>
        /// AniDB Character ID
        /// ex. 62620
        /// </summary>
        public int AniDBID { get; set; }

        /// <summary>
        /// Name, romanized
        /// ex. Shiro
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Alternate Name, this can be any other name, whether kanji, a nickname, etc
        /// ex. 白
        /// </summary>
        public string AlternateName { get; set; }

        /// <summary>
        /// Description, bio, etc
        /// ex. A truant, hikikomori, deadbeat game girl. The other-half member of 『　　』, and Sora's little sister.
        /// She's a beautiful girl with red eyes and pure white hair.
        /// She's a genius who learned to speak in just one year after birth, mastered multiple languages and advanced
        /// mathematics by age 3, and is capable of calculating future events to a degree that's almost clairvoyant.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The path to the image
        /// </summary>
        public string ImagePath { get; set; }
    }
}