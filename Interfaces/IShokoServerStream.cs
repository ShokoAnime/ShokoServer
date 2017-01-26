using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/Stream")]
    public interface IShokoServerStream
    {
        [Rest("/{videolocalid}/{userId?}/{autowatch?}/{fakename?}", Verbs.Get)]
        System.IO.Stream StreamVideo(int videolocalid, int? userId, bool? autowatch, string fakename);
        [Rest("Filename/{base64filename}/{userId?}/{autowatch?}/{fakename?}", Verbs.Get)]
        System.IO.Stream StreamVideoFromFilename(string base64filename, int? userId, bool? autowatch, string fakename);
        [Rest("/{videolocalid}/{userId?}/{autowatch?}/{fakename?}", Verbs.Head)]
        System.IO.Stream InfoVideo(int videolocalid, int? userId, bool? autowatch, string fakename);
        [Rest("Filename/{base64filename}/{userId?}/{autowatch?}/{fakename?}", Verbs.Head)]
        System.IO.Stream InfoVideoFromFilename(string base64filename, int? userId, bool? autowatch, string fakename);

    }
}
