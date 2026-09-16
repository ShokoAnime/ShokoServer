using System;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// The airing schedule provider a schedule or an airing came from. The name is
/// kept even after the plugin providing it is uninstalled, so a client always
/// has something to show.
/// </summary>
/// <param name="id">The ID of the provider.</param>
/// <param name="name">The name of the provider.</param>
public class AiringSource(Guid id, string name)
{
    /// <summary>
    /// The ID of the provider that owns the schedule or airing.
    /// </summary>
    [Required]
    public Guid ID { get; init; } = id;

    /// <summary>
    /// The name of the provider that owns the schedule or airing.
    /// </summary>
    [Required]
    public string Name { get; init; } = name;
}
