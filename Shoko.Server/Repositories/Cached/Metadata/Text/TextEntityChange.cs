using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Repositories.Cached.Metadata.Text;

/// <summary>
///   What one write changed among an entry's stored texts.
/// </summary>
/// <param name="EntityID">The entry.</param>
/// <param name="Kinds">Which kinds of text changed.</param>
/// <param name="Sources">The sources whose texts changed.</param>
internal sealed record TextEntityChange(MetadataGuid EntityID, IReadOnlySet<TextKind> Kinds, IReadOnlySet<MetadataSource> Sources);
