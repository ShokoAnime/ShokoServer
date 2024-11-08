using System.Collections.Generic;

namespace Shoko.Server.Settings;

public class PlexSettings
{

    public List<int> Libraries { get; set; } = [];

    public string Server { get; set; } = string.Empty;
}
