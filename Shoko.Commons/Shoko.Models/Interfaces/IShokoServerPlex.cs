using System.Collections.Generic;
using Nancy.Rest.Annotations.Attributes;
using Nancy.Rest.Annotations.Enums;
using Shoko.Models.Plex.Connections;
using Directory = Shoko.Models.Plex.Libraries.Directory;

namespace Shoko.Models.Interfaces
{
    [RestBasePath("/api/Plex")]
    public interface IShokoServerPlex
    {
        [Rest("Linking/Devices/Current/{userId}", Verbs.Get, TimeOutSeconds = 600)]
        MediaDevice CurrentDevice(int userId);

        [Rest("Linking/Directories/{userId}", Verbs.Post)]
        void UseDirectories(int userId, List<Directory> directories);

        [Rest("Linking/Directories/{userId}", Verbs.Get, TimeOutSeconds = 600)]
        Directory[] Directories(int userId);

        [Rest("Linking/Servers/{userId}", Verbs.Post)]
        void UseDevice(int userId, MediaDevice server);

        [Rest("Linking/Devices/{userId}", Verbs.Get, TimeOutSeconds = 600)]
        MediaDevice[] AvailableDevices(int userId);
    }
}
