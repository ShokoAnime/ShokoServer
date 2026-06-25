#nullable enable
using System.Collections.Generic;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Server.Models.Release;

public class EmbeddedCrossReference : IReleaseVideoCrossReference
{
    /// <inheritdoc/>
    public Dictionary<string, string> ProviderIDs { get; set; } = [];

    /// <inheritdoc/>
    public int PercentageStart { get; set; }

    /// <inheritdoc/>
    public int PercentageEnd { get; set; } = 100;

    public EmbeddedCrossReference() { }

    public EmbeddedCrossReference(IReleaseVideoCrossReference crossReference)
    {
        ProviderIDs = new Dictionary<string, string>(crossReference.ProviderIDs);
        PercentageStart = crossReference.PercentageStart;
        PercentageEnd = crossReference.PercentageEnd;
    }

    IReadOnlyDictionary<string, string> IReleaseVideoCrossReference.ProviderIDs => ProviderIDs;
}
