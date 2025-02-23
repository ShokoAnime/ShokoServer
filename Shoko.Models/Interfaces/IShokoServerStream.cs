using Nancy.Rest.Annotations.Attributes;
using Nancy.Rest.Annotations.Enums;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/Stream")]
    public interface IShokoServerStream
    {
        [Rest("/{videolocalid}/{userId?}/{autowatch?}/{fakename?}", Verbs.Get)]
        object StreamVideo(int videolocalid, int? userId, bool? autowatch, string fakename);
        [Rest("Filename/{base64filename}/{userId?}/{autowatch?}/{fakename?}", Verbs.Get)]
        object StreamVideoFromFilename(string base64filename, int? userId, bool? autowatch, string fakename);
        [Rest("/{videolocalid}/{userId?}/{autowatch?}/{fakename?}", Verbs.Head)]
        object InfoVideo(int videolocalid, int? userId, bool? autowatch, string fakename);
        [Rest("Filename/{base64filename}/{userId?}/{autowatch?}/{fakename?}", Verbs.Head)]
        object InfoVideoFromFilename(string base64filename, int? userId, bool? autowatch, string fakename);

    }
}
