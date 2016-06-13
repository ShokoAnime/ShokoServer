namespace JMMContracts
{
    public class Contract_ServerStatus
    {
        public int HashQueueCount { get; set; }
        public string HashQueueState { get; set; }
        public int GeneralQueueCount { get; set; }
        public string GeneralQueueState { get; set; }
        public int ImagesQueueCount { get; set; }
        public string ImagesQueueState { get; set; }
        public bool IsBanned { get; set; }
        public string BanReason { get; set; }
        public string BanOrigin { get; set; }
    }
}