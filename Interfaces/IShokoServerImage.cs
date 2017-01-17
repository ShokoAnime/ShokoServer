using System.IO;
using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/Image")]
    public interface IShokoServerImage
    {
        [Rest("{imageid}/{imageType}/{thumnbnailOnly?}", Verbs.Get)]
        Stream GetImage(int imageid, int imageType, bool? thumnbnailOnly);

        [Rest("WithPath/{serverImagePath}", Verbs.Get)]
        Stream GetImageUsingPath(string serverImagePath);

        [Rest("Blank", Verbs.Get)]
        Stream BlankImage();

        [Rest("Support/{name}/{ratio}", Verbs.Get)]
        Stream GetSupportImage(string name, float? ratio);

        [Rest("Thumb/{imageId}/{imageType}/{ratio}", Verbs.Get)]
        Stream GetThumb(int imageId, int imageType, float ratio);

        [Rest("Path/{imageId}/{imageType}/{thumnbnailOnly?}", Verbs.Get)]
        string GetImagePath(int imageId, int imageType, bool? thumnbnailOnly);

    }
}