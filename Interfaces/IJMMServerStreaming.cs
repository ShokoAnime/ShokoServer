using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/MetroDownload")]
    public interface IJMMServerStreaming
    {
        [Rest("Download/{filename}",Verbs.Get)]
        System.IO.Stream Download(string fileName);
    }
}