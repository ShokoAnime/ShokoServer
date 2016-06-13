namespace JMMServer.ImageDownload
{
    public class ImageDownloadRequest
    {
        public JMMImageType ImageType { get; set; }
        public object ImageData { get; set; }
        public bool ForceDownload { get; set; }

        public ImageDownloadRequest(JMMImageType imageType, object data, bool forceDownload)
        {
            this.ImageType = imageType;
            this.ImageData = data;
            this.ForceDownload = forceDownload;
        }
    }
}