
#nullable enable
namespace Shoko.Server.API.v3.Models.Release.Input;

public class UpdateSingleProviderBody
{
    public int? Priority { get; set; }

    public bool? IsEnabled { get; set; }
}
