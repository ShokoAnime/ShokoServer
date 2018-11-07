namespace Shoko.Server.Providers.AniDB
{
    public class NotifyListHeader
    {
        private string notifyType = string.Empty;

        public string NotifyType
        {
            get { return notifyType; }
            set { notifyType = value; }
        }

        private long notifyID = 0;

        public long NotifyID
        {
            get { return notifyID; }
            set { notifyID = value; }
        }
    }
}