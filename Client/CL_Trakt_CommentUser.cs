namespace Shoko.Models.Client
{
    public class CL_Trakt_CommentUser
    {
        // user details
        public CL_Trakt_User User { get; set; }
        // Comment details
        public CL_Trakt_Comment Comment { get; set; }
    }
}