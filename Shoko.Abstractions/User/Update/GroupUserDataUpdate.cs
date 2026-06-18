using System.Collections.Generic;

namespace Shoko.Abstractions.User.Update;

/// <summary>
///   Represents an update to the user-specific data associated with a group.
/// </summary>
public class GroupUserDataUpdate
{
    /// <summary>
    ///   Override the unique tags assigned to the group by the user.
    /// </summary>
    public IEnumerable<string>? UserTags { get; set; }
}
