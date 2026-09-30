using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.User.Enums;
using Shoko.Abstractions.User.Events;

namespace Shoko.Server.API.SignalR.Models;

public class UserChangedSignalRModel(UserChangedEventArgs args)
{
    /// <summary>
    /// The ID of the user.
    /// </summary>
    public int UserID { get; } = args.User.LocalID;

    /// <summary>
    /// What changed on the user.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public UserSaveReason Reason { get; } = args.Reason;
}
