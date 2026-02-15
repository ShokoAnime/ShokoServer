
using Shoko.Abstractions.Events;

namespace Shoko.Server.API.SignalR.Models;

public class UserChangedSignalRModel(UserChangedEventArgs args)
{
    /// <summary>
    /// The ID of the folder.
    /// </summary>
    public int UserID { get; } = args.User.ID;
}
