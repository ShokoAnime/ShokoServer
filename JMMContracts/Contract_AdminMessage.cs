namespace Shoko.Models
{
    public class Contract_AdminMessage
    {
        public int AdminMessageId { get; set; }
        public long MessageDate { get; set; }
        public int MessageType { get; set; }
        public string Message { get; set; }
        public string MessageURL { get; set; }
    }
}