using System.ServiceModel;

namespace Shoko.Models
{
    public interface IJMMServerStreaming
    {
        System.IO.Stream Download(string fileName);
    }
}