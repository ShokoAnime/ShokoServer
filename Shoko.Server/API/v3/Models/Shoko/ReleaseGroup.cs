
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.API.v3.Models.Shoko;


public class ReleaseGroup
{
    /// <summary>
    /// AniDB release group ID (69)
    /// /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// The Release Group's Name (Unlimited Translation Works)
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The Release Group's Name (UTW)
    /// </summary>
    public string ShortName { get; set; }

    /// <summary>
    /// Source. Anidb, User, etc.
    /// </summary>
    /// <value></value>
    public string Source { get; set; }

    public ReleaseGroup(AniDB_ReleaseGroup group)
    {
        ID = group.GroupID;
        Name = group.GroupName;
        ShortName = group.GroupNameShort;
        Source = "AniDB";
    }
}
