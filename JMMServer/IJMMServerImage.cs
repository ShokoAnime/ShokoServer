using System.ServiceModel;

namespace Shoko.Models
{
    public interface IJMMServerImage
    {
        byte[] GetImage(string entityID, int entityType, bool thumnbnailOnly);

        byte[] GetImageUsingPath(string serverImagePath);
    }
}