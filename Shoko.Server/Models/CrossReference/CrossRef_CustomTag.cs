
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_CustomTag
{
    public int CrossRef_CustomTagID { get; set; }

    public int CustomTagID { get; set; }

    public int CrossRefID { get; set; }

    public int CrossRefType { get; set; } = 1;
}
