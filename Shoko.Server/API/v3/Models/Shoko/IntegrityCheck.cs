
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

// TODO: Improve this once we're bringing back the integrity check feature.
public class IntegrityCheck
{
    [Required]
    public int ID { get; set; }

    [Required]
    public List<int> ManagedFolderIDs { get; set; } = [];

    [Required, JsonConverter(typeof(StringEnumConverter))]
    public ScanStatus Status { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }
}
