using System.ServiceModel;

namespace Shoko.Models
{
    [ServiceContract]
    public interface IJMMServerImage
    {
        [OperationContract]
        byte[] GetImage(string entityID, int entityType, bool thumnbnailOnly);

        [OperationContract]
        byte[] GetImageUsingPath(string serverImagePath);
    }
}