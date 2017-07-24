namespace AniDBAPI
{
    public class NotifyListHeader
    {
        private NotifyListHeader()
        {
            NotifyID = 0;
            NotifyType = "";
        }

        public string NotifyType { get; set; }

        public long NotifyID { get; set; }
    }
}