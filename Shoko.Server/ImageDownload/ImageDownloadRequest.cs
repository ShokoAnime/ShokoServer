using Shoko.Models.Enums;

namespace Shoko.Server.ImageDownload
{
    public class ImageDownloadRequest
    {
        public ImageEntityType ImageType { get; set; }
        public object ImageData { get; set; }
        public bool ForceDownload { get; set; }

        public ImageDownloadRequest(ImageEntityType imageType, object data, bool forceDownload)
        {
            this.ImageType = imageType;
            this.ImageData = data;
            this.ForceDownload = forceDownload;
        }
    }
}