using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A tag as it applies to one entry. Read as a tag, it is the stored tag
///   with the entry's own weight and spoiler flag.
/// </summary>
public class Metadata_Tag_Entry : MetadataEntryRow, ITag, IMetadataStoreRow<Metadata_Tag_Entry>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_Tag_EntryID { get; set; }

    /// <summary>
    ///   The store's ID for the tag.
    /// </summary>
    public int TagID { get; set; }

    /// <summary>
    ///   How strongly the tag applies, on the source's own scale.
    /// </summary>
    public int? Weight { get; set; }

    /// <summary>
    ///   Whether the tag gives something away for this entry in particular.
    /// </summary>
    public bool IsSpoiler { get; set; }

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Tag_Entry>.RowID
    {
        get => Metadata_Tag_EntryID;
        set => Metadata_Tag_EntryID = value;
    }

    Metadata_Tag_Entry IMetadataStoreRow<Metadata_Tag_Entry>.Clone()
        => (Metadata_Tag_Entry)MemberwiseClone();

    #endregion

    #region Navigation

    /// <summary>
    ///   The stored tag.
    /// </summary>
    public Metadata_Tag? Tag => TagID > 0 ? RepoFactory.Metadata_Tag.GetByID(TagID) : null;

    #endregion

    #region ITag Implementation

    // The row's source and kind name its entry; tags are never removed, so the
    // row always has one.
    MetadataGuid IMetadata.ID => new(Source, MetadataEntityType.Tag, Tag?.ProviderID ?? throw new InvalidOperationException($"The stored tag {TagID} is missing."));

    MetadataSource IMetadata.Source => Source;

    MetadataEntityType IMetadata.EntityType => MetadataEntityType.Tag;

    string ITag.Name => Tag?.Name ?? string.Empty;

    string ITag.Overview => Tag?.Description ?? string.Empty;

    TagKind ITag.Kind => Tag?.Kind ?? TagKind.Tag;

    string? ITag.Category => Tag?.Category;

    bool ITag.IsSpoiler => IsSpoiler || (Tag?.IsSpoiler ?? false);

    bool ITag.IsRestricted => Tag?.IsRestricted ?? false;

    int? ITag.Weight => Weight;

    #endregion
}
