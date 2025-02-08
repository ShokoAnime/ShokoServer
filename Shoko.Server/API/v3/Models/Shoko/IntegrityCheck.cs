
using System;
using System.Collections.Generic;

namespace Shoko.Server.API.v3.Models.Shoko;

// TODO: Improve this once we're bringing back the integrity check feature.
public class IntegrityCheck
{
    public int ID { get; set; }

    public List<int> ImportFolderIDs { get; set; }

    public int Status { get; set; }

    public DateTime CreatedAt { get; set; }
}
