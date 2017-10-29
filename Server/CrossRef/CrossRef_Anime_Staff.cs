namespace Shoko.Models.Server
{
    /// <summary>
    /// CrossRef Model for getting the relationships between Anime, Characters, and Staff
    /// </summary>
    public class CrossRef_Anime_Staff
    {
        /// <summary>
        /// Internal ID
        /// </summary>
        public int CrossRef_Anime_StaffID { get; set; }

        /// <summary>
        /// AniDB_AnimeID
        /// </summary>
        public int AniDB_AnimeID { get; set; }

        /// <summary>
        /// StaffID
        /// </summary>
        public int StaffID { get; set; }

        /// <summary>
        /// Character role type, or any other details about the role they filled
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// For seiyuus, it'll be character ID.
        /// </summary>
        public int? RoleID { get; set; }

        /// <summary>
        /// Enums.StaffRoleType representing the kind of role that the staff plays
        /// </summary>
        public int RoleType { get; set; }

        /// <summary>
        /// Default Japanese, only different if it is dub specific staff
        /// </summary>
        public string Language { get; set; }
    }
}