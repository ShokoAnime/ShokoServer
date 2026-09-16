using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Airing;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing.Input;

/// <summary>
/// The changes to apply to a single airing schedule provider.
/// </summary>
public class UpdateSingleProviderBody
{
    /// <summary>
    /// Optional. The new source order of the provider.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Optional. The kinds the provider is allowed to supply. An empty list
    /// disables the provider, and a kind the provider cannot supply is
    /// rejected.
    /// </summary>
    public List<AiringKind>? EnabledKinds { get; set; }
}
