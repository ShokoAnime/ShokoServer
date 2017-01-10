namespace Shoko.Models.Client
{
    public class CL_ServerStatus
    {
        public int HashQueueCount { get; set; }
        public string HashQueueState { get; set; }  //Deprecated since 3.6.0.0
        public int HashQueueStateId { get; set; }
        public string[] HashQueueStateParams { get; set; }
        public int GeneralQueueCount { get; set; }
        public string GeneralQueueState { get; set; } //Deprecated since 3.6.0.0
        public int GeneralQueueStateId { get; set; }
        public string[] GeneralQueueStateParams { get; set; }
        public int ImagesQueueCount { get; set; }
        public string ImagesQueueState { get; set; } //Deprecated since 3.6.0.0
        public int ImagesQueueStateId { get; set; }
        public string[] ImagesQueueStateParams { get; set; }
        public bool IsBanned { get; set; }
        public string BanReason { get; set; }
        public string BanOrigin { get; set; }
    }
}