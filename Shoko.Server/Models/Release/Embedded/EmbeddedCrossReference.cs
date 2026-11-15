using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Utilities;

namespace Shoko.Server.Models.Release;

public class EmbeddedCrossReference : IReleaseVideoCrossReference
{
    private MetadataGuid[] _ids = [];

    /// <inheritdoc/>
    /// <remarks>
    ///   Kept as one array of pooled instances, since the same IDs repeat
    ///   across every release of a series, ordered with the main entry first
    ///   (<see cref="ReleaseCrossReferenceIDComparer"/>).
    /// </remarks>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public IReadOnlyList<MetadataGuid> IDs
    {
        get => _ids;
        set => _ids = value?.Where(id => id is not null).Select(id => MetadataGuidPool.Get(id)).Order(ReleaseCrossReferenceIDComparer.Instance).ToArray() ?? [];
    }

    /// <inheritdoc/>
    // MySQL and MariaDB wrote these as floats when the cross-references were rewritten (schema step 205/1).
    [JsonConverter(typeof(WholeNumberJsonConverter))]
    public int PercentageStart { get; set; }

    /// <inheritdoc/>
    [JsonConverter(typeof(WholeNumberJsonConverter))]
    public int PercentageEnd { get; set; } = 100;

    public EmbeddedCrossReference() { }

    public EmbeddedCrossReference(IReleaseVideoCrossReference crossReference)
    {
        IDs = crossReference.IDs;
        PercentageStart = crossReference.PercentageStart;
        PercentageEnd = crossReference.PercentageEnd;
    }
}
