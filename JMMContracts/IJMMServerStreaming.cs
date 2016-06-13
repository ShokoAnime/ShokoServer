using System.IO;
using System.ServiceModel;

namespace JMMContracts
{
    [ServiceContract]
    public interface IJMMServerStreaming
    {
        [OperationContract]
        Stream Download(string fileName);
    }
}