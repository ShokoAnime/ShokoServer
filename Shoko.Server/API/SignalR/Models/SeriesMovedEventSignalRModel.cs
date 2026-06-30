using Shoko.Abstractions.Metadata.Events;

namespace Shoko.Server.API.SignalR.Models;

public class SeriesMovedEventSignalRModel
{
    public SeriesMovedEventSignalRModel(SeriesMovedEventArgs eventArgs)
    {
        SeriesID = eventArgs.SeriesInfo.ID;
        OldGroupID = eventArgs.OldGroupID;
        NewGroupID = eventArgs.NewGroupID;
    }

    public int SeriesID { get; }

    public int OldGroupID { get; }

    public int NewGroupID { get; }
}
