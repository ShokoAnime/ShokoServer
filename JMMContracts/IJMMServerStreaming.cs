using System.ServiceModel;

namespace Shoko.Models
{
    [ServiceContract]
    public interface IJMMServerStreaming
    {
        [OperationContract]
        System.IO.Stream Download(string fileName);
    }
}