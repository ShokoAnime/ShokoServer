namespace Shoko.Server.API.v3.Models.Shoko
{
    public class User
    {
        /// <summary>
        /// The UserID, this is used in a lot of v1 and v2 endpoints, and it's needed for editing or removing a user
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Pretty Self-explanatory. It's the Username of the user
        /// </summary>
        public string Username { get; set; }

        public class FullUser
        {
            /// <summary>
            /// The password...Shoko is NOT secure, so don't assume this password is safe or even necessary to access the account
            /// </summary>
            public string Password { get; set; }
        }
    }
}