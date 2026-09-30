using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.API.v3.Models.TextManagement.Input;

/// <summary>
///   Body for changing several stored texts at once, each in its own way.
/// </summary>
public class BatchUpdateTextBody
{
    /// <summary>
    ///   The texts to change, and what to change on each.
    /// </summary>
    [Required, MinLength(1)]
    public List<Item> Texts { get; set; } = [];

    /// <summary>
    ///   One stored text to change. Every member left out or <c>null</c>
    ///   keeps its current value.
    /// </summary>
    public class Item : UpdateTextBody
    {
        /// <summary>
        ///   Whether the ID names a title or an overview. Required: title and
        ///   overview IDs overlap, so the kind is never guessed.
        /// </summary>
        [Required]
        public TextKind? Kind { get; set; }

        /// <summary>
        ///   The text's ID.
        /// </summary>
        [Required, Range(1, int.MaxValue)]
        public int? ID { get; set; }
    }
}
