using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Interfaces;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A tag or genre a source keeps in the tag store.
/// </summary>
public class Metadata_Tag : ITag, IMetadataStoreRow<Metadata_Tag>, IInlineTextSource
{
    #region Database Columns

    /// <summary>
    ///   The store's own ID for the tag, the same across sources.
    /// </summary>
    public int Metadata_TagID { get; set; }

    /// <summary>
    ///   The source the tag belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the tag.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   The tag's name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///   What the tag means.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///   Whether it is a genre or a descriptive tag.
    /// </summary>
    public TagKind Kind { get; set; }

    /// <summary>
    ///   The group the source files the tag under.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    ///   Whether the tag gives something away wherever it is used.
    /// </summary>
    public bool IsSpoiler { get; set; }

    /// <summary>
    ///   Whether the tag is for adult content.
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    ///   When the source last wrote the tag.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Tag>.RowID
    {
        get => Metadata_TagID;
        set => Metadata_TagID = value;
    }

    Metadata_Tag IMetadataStoreRow<Metadata_Tag>.Clone()
        => (Metadata_Tag)MemberwiseClone();

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(Source, MetadataEntityType.Tag, ProviderID);

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => InlineText.Title(Source, Name, TitleLanguage.Unknown, "unk");

    IText? IInlineTextSource.InlineOverview => InlineText.Overview(Source, Description, TitleLanguage.English, "en");

    #endregion

    #region ITag Implementation

    string ITag.Overview => Description;

    // Read outside any entity, so it has no weight there.
    int? ITag.Weight => null;

    #endregion
}
