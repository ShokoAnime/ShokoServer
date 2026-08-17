
namespace Shoko.Server.Models.Internal;

public class Versions
{
    public int VersionsID { get; set; }

    public string VersionType { get; set; } = null!;

    public string VersionValue { get; set; } = null!;

    public string VersionRevision { get; set; } = null!;

    public string VersionCommand { get; set; } = null!;

    public string VersionProgram { get; set; } = null!;
}
