using System;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   One crew credit on an entry.
/// </summary>
public class Metadata_Crew : MetadataEntryRow, ICrew<ISeries>, ICrew<ISeason>, ICrew<IEpisode>, ICrew<IMovie>, IMetadataStoreRow<Metadata_Crew>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_CrewID { get; set; }

    /// <summary>
    ///   The store's ID for the creator credited.
    /// </summary>
    public int CreatorID { get; set; }

    /// <summary>
    ///   The job, as the source writes it.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///   The kind of job, where the source's wording maps onto one.
    /// </summary>
    public CrewRoleType RoleType { get; set; }

    /// <summary>
    ///   The language the job was done in, as a language code, when the
    ///   source gave one.
    /// </summary>
    public string? LanguageCode { get; set; }

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Crew>.RowID
    {
        get => Metadata_CrewID;
        set => Metadata_CrewID = value;
    }

    Metadata_Crew IMetadataStoreRow<Metadata_Crew>.Clone()
        => (Metadata_Crew)MemberwiseClone();

    #endregion

    #region Navigation

    /// <summary>
    ///   The creator credited, when stored.
    /// </summary>
    public Metadata_Creator? Creator => CreatorID > 0 ? RepoFactory.Metadata_Creator.GetByID(CreatorID) : null;

    /// <summary>
    ///   The language the job was done in, or unknown when the source did not
    ///   give one.
    /// </summary>
    public TitleLanguage Language => LanguageCode is { Length: > 0 } code && code.TryGetTitleLanguage(out var language) ? language : TitleLanguage.Unknown;

    #endregion

    #region ICrew Implementation

    MetadataGuid ICrew.CreatorID => ((IMetadata?)Creator)?.ID ?? throw new InvalidOperationException($"The stored creator {CreatorID} is missing.");

    MetadataGuid ICrew.ParentID => EntryID;

    string ICrew.LanguageCode => LanguageCode ?? Language.GetString();

    IMetadata? ICrew.Parent => GetEntry();

    ICreator? ICrew.Creator => Creator;

    ISeries? ICrew<ISeries>.ParentOfType => GetEntry() as ISeries;

    ISeason? ICrew<ISeason>.ParentOfType => GetEntry() as ISeason;

    IEpisode? ICrew<IEpisode>.ParentOfType => GetEntry() as IEpisode;

    IMovie? ICrew<IMovie>.ParentOfType => GetEntry() as IMovie;

    #endregion
}
