using System.ServiceModel;

namespace JMMContracts
{
    [ServiceContract]
    public interface IJMMServerStreaming
    {
        [OperationContract]
        System.IO.Stream Download(string fileName);
    }
}