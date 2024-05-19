using System;

namespace Shoko.Server.Models;

public class AniDB_Notification
{
    public int AniDB_NotificationID { get; set; }
    public int NotificationID { get; set; }
    public int RelatedTypeID { get; set; }
    public int NotificationType { get; set; }
    public int CountPending { get; set; }
    public DateTime Date { get; set; }
    public string RelatedTypeName { get; set; }
    public string FileIds { get; set; }
}
