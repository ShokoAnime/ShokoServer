
namespace Shoko.Server.API.v1.Models;

public class CL_ImportFolder
{
    public int ImportFolderID { get; set; }
    public int ImportFolderType { get; set; }
    public string ImportFolderName { get; set; }
    public string ImportFolderLocation { get; set; }
    public int IsWatched { get; set; }
    public int IsDropSource { get; set; }
    public int IsDropDestination { get; set; }
}
