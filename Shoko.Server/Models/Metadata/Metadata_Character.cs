using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A character a source keeps in the people store.
/// </summary>
public class Metadata_Character : ICharacter, IMetadataStoreRow<Metadata_Character>, IInlineTextSource, IMetadataStubRow, IMetadataDefaultImageSource
{
    #region Database Columns

    /// <summary>
    ///   The store's own ID for the character, the same across sources.
    /// </summary>
    public int Metadata_CharacterID { get; set; }

    /// <summary>
    ///   The source the character belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the character.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   The character's name, as the source writes it for most readers.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///   The character's name in its original script, when it differs.
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    ///   What the source says about the character.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///   Whether it is a character or an organization.
    /// </summary>
    public CharacterType Type { get; set; }

    /// <summary>
    ///   The character's gender, when the source says.
    /// </summary>
    public PersonGender Gender { get; set; }

    /// <summary>
    ///   The character's birthday, when the source says. Any part of it
    ///   may be unknown, so it may be only a year, or only a month and a day.
    /// </summary>
    public FuzzyDateOnly? BirthDay { get; set; }

    /// <summary>
    ///   The links to the character elsewhere that its source gave.
    /// </summary>
    public List<Resource> Resources { get; set; } = [];

    /// <summary>
    ///   When the store first wrote the character, stub or not. Set once and never
    ///   changed.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///   When the source last wrote the character, or <c>null</c> for a stub
    ///   the core made for a credit before the source wrote it.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>
    ///   Since when nothing has credited the character, or <c>null</c> while
    ///   something does. The purge of orphaned metadata goes by it.
    /// </summary>
    public DateTime? LastOrphanedAt { get; set; }

    /// <summary>
    ///   When the core last asked the source to refresh the character, found or
    ///   not, in local time, or <c>null</c> when it never did. Kept by the
    ///   entity refresh job alone; a save of the character keeps it.
    /// </summary>
    public DateTime? LastRefreshedAt { get; set; }

    /// <summary>
    ///   What the source said of the character that needs no column of its
    ///   own, or <c>null</c> when it said none of it.
    /// </summary>
    public Metadata_CharacterExtra? ExtraData { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   Whether the character is a stub: a row the core made, with only the
    ///   name a credit carried, before its source wrote it. The source's
    ///   next save of the character fills it in.
    /// </summary>
    public bool IsStub => LastUpdatedAt is null;

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Character>.RowID
    {
        get => Metadata_CharacterID;
        set => Metadata_CharacterID = value;
    }

    Metadata_Character IMetadataStoreRow<Metadata_Character>.Clone()
        => (Metadata_Character)MemberwiseClone();

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(Source, MetadataEntityType.Character, ProviderID);

    #endregion

    #region IWithUpdateDate Implementation

    // A stub was never written by its source.
    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdatedAt ?? DateTime.UnixEpoch;

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => InlineText.Title(Source, Name, TitleLanguage.Unknown, "unk");

    IText? IInlineTextSource.InlineOverview => InlineText.Overview(Source, Description, TitleLanguage.English, "en");

    #endregion

    #region IWithOverviews Implementation

    IText? IWithOverviews.DefaultOverview => TextAccess.Manager.DefaultOverviewFor(this);

    IText? IWithOverviews.PreferredOverview => TextAccess.Manager.PreferredOverviewFor(this) ?? TextAccess.Manager.DefaultOverviewFor(this);

    IReadOnlyList<IText> IWithOverviews.Overviews => TextAccess.Manager.ListOverviews(this);

    #endregion

    #region IMetadataDefaultImageSource Implementation

    string? IMetadataDefaultImageSource.GetDefaultResourceID(ImageEntityType imageType)
        => ExtraData?.GetDefaultResourceID(imageType);

    #endregion

    #region IWithImages Implementation

    /// <summary>
    ///   The primary image the character's own source pins as its default,
    ///   else the first one it gave it.
    /// </summary>
    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Primary);

    #endregion

    #region IWithResources Implementation

    IReadOnlyList<Resource> IWithResources.Resources
        => [.. Resources, .. ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this)];

    #endregion

    #region ICharacter Implementation

    DateTime? ICharacter.LastRefreshedAt => LastRefreshedAt?.ToUniversalTime();

    IReadOnlyList<ITitle> ICharacter.AlternativeNames => TextAccess.Manager.AlternativeNamesOf(this);

    IEnumerable<ICast<IEpisode>> ICharacter.EpisodeCastRoles => CastRoles(MetadataEntityType.Episode);

    IEnumerable<ICast<IMovie>> ICharacter.MovieCastRoles => CastRoles(MetadataEntityType.Movie);

    IEnumerable<ICast<ISeries>> ICharacter.SeriesCastRoles => CastRoles(MetadataEntityType.Series);

    private IEnumerable<Metadata_Cast> CastRoles(MetadataEntityType entityType)
        => MetadataEntryRow.InOrder(RepoFactory.Metadata_Cast.GetByCharacterID(Metadata_CharacterID).Where(cast => cast.EntityType == entityType));

    #endregion
}
