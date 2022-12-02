using System.ComponentModel.DataAnnotations;
using Shoko.Models.Plex.Connections;
using Shoko.Models.Plex.Libraries;

# nullable enable
namespace Shoko.Server.API.v3.Plex;

public class PlexLibrary
{
    public PlexLibrary()
    {
        ID = 0;
        ServerID = "";
        Name = "";
        IsActive = false;
    }

    public PlexLibrary(Directory directory, MediaDevice device, bool selected = false)
    {
        ID = directory.Key;
        ServerID = device.ClientIdentifier;
        Name = directory.Title;
        IsActive = selected;
    }

    /// <summary>
    /// The library id relative to the plex server it belongs to.
    /// </summary>
    [Required]
    public int ID { get; set; }

    /// <summary>
    /// The <see cref="PlexServer"/>'s ID.
    /// </summary>
    [Required]
    public string ServerID { get; set; }

    /// <summary>
    /// The name of the library.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// True if the library is currently being synced.
    /// </summary>
    public bool IsActive { get; set; }
}
