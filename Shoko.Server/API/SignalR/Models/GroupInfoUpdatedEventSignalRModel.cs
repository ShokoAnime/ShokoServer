using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

public class GroupInfoUpdatedEventSignalRModel
{
    public GroupInfoUpdatedEventSignalRModel(GroupInfoUpdatedEventArgs eventArgs)
    {
        Reason = eventArgs.Reason;
        GroupID = eventArgs.GroupInfo.ID;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public UpdateReason Reason { get; }

    public int GroupID { get; }
}
