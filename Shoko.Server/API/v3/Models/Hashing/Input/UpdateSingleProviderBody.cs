using System.Collections.Generic;

namespace Shoko.Server.API.v3.Models.Hashing.Input;

public class UpdateSingleProviderBody
{
    public HashSet<string>? EnabledHashTypes { get; set; }
}
