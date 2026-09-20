using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Action;

/// <summary>
/// Body for invoking an action across several entities of the same type at
/// once.
/// </summary>
public class ActionBulkInvokeBody
{
    /// <summary>
    /// The IDs of the entities to invoke the action on. Validation failures
    /// are reported against this list by position, as <c>IDs[0]</c>,
    /// <c>IDs[1]</c> and so on.
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<int> IDs { get; set; } = [];

    /// <summary>
    /// Optional. The action's free-form invocation parameters, applied to
    /// every entity in <see cref="IDs"/>. Supported values are booleans,
    /// numbers, and string lists; no nested objects. Entries with no matching
    /// property on the action are ignored.
    /// </summary>
    public Dictionary<string, object?>? Parameters { get; set; } = null;
}
