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
///   A creator a source keeps in the people store: a person or company
///   credited on its entries.
/// </summary>
public class Metadata_Creator : ICreator, IMetadataStoreRow<Metadata_Creator>, IInlineTextSource, IMetadataStubRow, IMetadataDefaultImageSource
{
    #region Database Columns

    /// <summary>
    ///   The store's own ID for the creator, the same across sources.
    /// </summary>
    public int Metadata_CreatorID { get; set; }

    /// <summary>
    ///   The source the creator belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the creator.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   The creator's name, as the source writes it for most readers.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///   The creator's name in its original script, when it differs.
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    ///   What the source says about the creator.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///   Whether the creator is a person, a company or something else.
    /// </summary>
    public CreatorType Type { get; set; }

    /// <summary>
    ///   The creator's gender, when the source says.
    /// </summary>
    public PersonGender Gender { get; set; }

    /// <summary>
    ///   When the creator was born, when the source says. Any part of it
    ///   may be unknown, so it may be only a year, or only a month and a day.
    /// </summary>
    public FuzzyDateOnly? BirthDay { get; set; }

    /// <summary>
    ///   When the creator died, when the source says. Any part of it may be
    ///   unknown.
    /// </summary>
    public FuzzyDateOnly? DeathDay { get; set; }

    /// <summary>
    ///   The links to the creator elsewhere that its source gave.
    /// </summary>
    public List<Resource> Resources { get; set; } = [];

    /// <summary>
    ///   Whether the source marks the creator as known for adult content
    ///   only.
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    ///   What the source said of the creator that needs no column of its
    ///   own, or <c>null</c> when it said none of it.
    /// </summary>
    public Metadata_CreatorExtra? ExtraData { get; set; }

    /// <summary>
    ///   When the store first wrote the creator, stub or not. Set once and never
    ///   changed.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///   When the source last wrote the creator, or <c>null</c> for a stub
    ///   the core made for a credit before the source wrote it.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>
    ///   Since when nothing has credited the creator, or <c>null</c> while
    ///   something does. The purge of orphaned metadata goes by it.
    /// </summary>
    public DateTime? LastOrphanedAt { get; set; }

    /// <summary>
    ///   When the core last asked the source to refresh the creator, found or
    ///   not, in local time, or <c>null</c> when it never did. Kept by the
    ///   entity refresh job alone; a save of the creator keeps it.
    /// </summary>
    public DateTime? LastRefreshedAt { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   Whether the creator is a stub: a row the core made, with only the
    ///   name a credit carried, before its source wrote it. The source's
    ///   next save of the creator fills it in.
    /// </summary>
    public bool IsStub => LastUpdatedAt is null;

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Creator>.RowID
    {
        get => Metadata_CreatorID;
        set => Metadata_CreatorID = value;
    }

    Metadata_Creator IMetadataStoreRow<Metadata_Creator>.Clone()
        => (Metadata_Creator)MemberwiseClone();

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(Source, MetadataEntityType.Creator, ProviderID);

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
    ///   The primary image the creator's own source pins as its default,
    ///   else the first one it gave it.
    /// </summary>
    public IImageCrossReference? DefaultPrimaryImageCrossReference
        => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Primary);

    #endregion

    #region IWithResources Implementation

    IReadOnlyList<Resource> IWithResources.Resources
        => [.. Resources, .. ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this)];

    #endregion

    #region ICreator Implementation

    DateTime? ICreator.LastRefreshedAt => LastRefreshedAt?.ToUniversalTime();

    IReadOnlyList<ITitle> ICreator.AlternativeNames => TextAccess.Manager.AlternativeNamesOf(this);

    string? ICreator.PlaceOfBirth => ExtraData?.PlaceOfBirth;

    IEnumerable<ICast<IEpisode>> ICreator.EpisodeCastRoles => CastRoles(MetadataEntityType.Episode);

    IEnumerable<ICast<IMovie>> ICreator.MovieCastRoles => CastRoles(MetadataEntityType.Movie);

    IEnumerable<ICast<ISeries>> ICreator.SeriesCastRoles => CastRoles(MetadataEntityType.Series);

    IEnumerable<ICrew<IEpisode>> ICreator.EpisodeCrewRoles => CrewRoles(MetadataEntityType.Episode);

    IEnumerable<ICrew<IMovie>> ICreator.MovieCrewRoles => CrewRoles(MetadataEntityType.Movie);

    IEnumerable<ICrew<ISeries>> ICreator.SeriesCrewRoles => CrewRoles(MetadataEntityType.Series);

    private IEnumerable<Metadata_Cast> CastRoles(MetadataEntityType entityType)
        => MetadataEntryRow.InOrder(RepoFactory.Metadata_Cast.GetByCreatorID(Metadata_CreatorID).Where(cast => cast.EntityType == entityType));

    private IEnumerable<Metadata_Crew> CrewRoles(MetadataEntityType entityType)
        => MetadataEntryRow.InOrder(RepoFactory.Metadata_Crew.GetByCreatorID(Metadata_CreatorID).Where(crew => crew.EntityType == entityType));

    #endregion
}
