using System.IO;
using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/Image")]
    public interface IJMMServerImage
    {
        [Rest("{entityID}/{entityType}/{thumnbnailOnly}",Verbs.Get)]
        Stream GetImage(string entityID, int entityType, bool thumnbnailOnly);

        [Rest("{serverImagePath}", Verbs.Get)]
        Stream GetImageUsingPath(string serverImagePath);
    }
}