using System.Collections.Generic;

#nullable enable
namespace Shoko.Server.API.v3.Models.Hashing.Input;

public class UpdateSingleProviderBody
{
    public HashSet<string>? EnabledHashTypes { get; set; }
}
