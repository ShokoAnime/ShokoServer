using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/Download")]
    public interface IShokoServerDownload
    {
        [Rest("Download/{fileName}",Verbs.Get)]
        System.IO.Stream Download(string fileName);
    }
}