using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

public class CollectionInfoUpdatedEventSignalRModel
{
    public CollectionInfoUpdatedEventSignalRModel(CollectionInfoUpdatedEventArgs eventArgs)
    {
        Source = eventArgs.CollectionInfo.Source;
        Reason = eventArgs.Reason;
        CollectionID = eventArgs.CollectionInfo.ID.ID;
    }

    /// <summary>
    /// The provider metadata source.
    /// </summary>
    public MetadataSource Source { get; }

    /// <summary>
    /// The update reason.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public UpdateReason Reason { get; }

    /// <summary>
    /// The provided metadata collection id.
    /// </summary>
    public string CollectionID { get; }
}
