using System.Collections.Generic;

#nullable enable
namespace Shoko.Server.API.v3.Models.Hashing.Input;

public class UpdateSingleProviderBody
{
    public int? Priority { get; set; }

    public HashSet<string>? EnabledHashTypes { get; set; }
}
