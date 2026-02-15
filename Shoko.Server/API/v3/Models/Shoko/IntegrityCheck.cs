
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.Server;

namespace Shoko.Server.API.v3.Models.Shoko;

// TODO: Improve this once we're bringing back the integrity check feature.
public class IntegrityCheck
{
    public int ID { get; set; }

    public List<int> ManagedFolderIDs { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public ScanStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
}
