using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing.Input;

/// <summary>
/// The channels to merge into another one.
/// </summary>
public class MergeChannelsBody
{
    /// <summary>
    /// The IDs of the channels to merge. Each must be of the target's type,
    /// and none may be the target itself.
    /// </summary>
    [Required, MinLength(1)]
    public List<Guid> SourceIDs { get; set; } = [];
}
