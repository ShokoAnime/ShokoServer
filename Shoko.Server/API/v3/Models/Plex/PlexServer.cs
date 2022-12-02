using System.ComponentModel.DataAnnotations;
using Shoko.Models.Plex.Connections;

#nullable enable
namespace Shoko.Server.API.v3.Plex;

public class PlexServer
{
    public PlexServer()
    {
        ID = "";
        Name = "";
        IsActive = false;
    }

    public PlexServer(MediaDevice device, bool isActive = false)
    {
        ID = device.ClientIdentifier;
        Name = device.Name;
        IsActive = isActive;
    }

    /// <summary>
    /// The unique id for the server.
    /// </summary>
    [Required]
    public string ID { get; set; }

    /// <summary>
    /// The display name of the server.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// True if this is the currently selected server.
    /// </summary>
    public bool IsActive { get; set; }
}
