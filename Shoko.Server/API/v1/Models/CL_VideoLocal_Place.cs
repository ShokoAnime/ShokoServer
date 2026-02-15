namespace Shoko.Server.API.v1.Models;

public class CL_VideoLocal_Place
{
    public int VideoLocal_Place_ID { get; set; }
    public int VideoLocalID { get; set; }
    public string FilePath { get; set; }
    public int ImportFolderID { get; set; }
    public int ImportFolderType { get; set; }
    public CL_ImportFolder ImportFolder { get; set; }

}
