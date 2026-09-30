using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   One cast credit on an entry: a character, who voiced or played it, or
///   both.
/// </summary>
public class Metadata_Cast : MetadataEntryRow, ICast<ISeries>, ICast<ISeason>, ICast<IEpisode>, ICast<IMovie>, IMetadataStoreRow<Metadata_Cast>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_CastID { get; set; }

    /// <summary>
    ///   The store's ID for the creator who voiced or played the role, when
    ///   known.
    /// </summary>
    public int? CreatorID { get; set; }

    /// <summary>
    ///   The store's ID for the character, when the credit has one.
    /// </summary>
    public int? CharacterID { get; set; }

    /// <summary>
    ///   The name the role is credited under.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///   How important the role is.
    /// </summary>
    public CastRoleType RoleType { get; set; }

    /// <summary>
    ///   The language of the performance, as a language code, when the source
    ///   gave one.
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    ///   Notes on the role, such as the age or form the character is played
    ///   in, when the source gave any.
    /// </summary>
    public string? RoleNotes { get; set; }

    /// <summary>
    ///   The group that made the dub the performance is in, when the source
    ///   named one.
    /// </summary>
    public string? DubGroup { get; set; }

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Cast>.RowID
    {
        get => Metadata_CastID;
        set => Metadata_CastID = value;
    }

    Metadata_Cast IMetadataStoreRow<Metadata_Cast>.Clone()
        => (Metadata_Cast)MemberwiseClone();

    #endregion

    #region Navigation

    /// <summary>
    ///   The creator who voiced or played the role, when known and stored.
    /// </summary>
    public Metadata_Creator? Creator => CreatorID is int creatorID and > 0 ? RepoFactory.Metadata_Creator.GetByID(creatorID) : null;

    /// <summary>
    ///   The character, when the credit has one and it is stored.
    /// </summary>
    public Metadata_Character? Character => CharacterID is int characterID and > 0 ? RepoFactory.Metadata_Character.GetByID(characterID) : null;

    /// <summary>
    ///   The language of the performance, or unknown when the source did not
    ///   give one.
    /// </summary>
    public TitleLanguage Language => LanguageCode is { Length: > 0 } code && code.TryGetTitleLanguage(out var language) ? language : TitleLanguage.Unknown;

    #endregion

    #region ICast Implementation

    MetadataGuid? ICast.CreatorID => ((IMetadata?)Creator)?.ID;

    MetadataGuid? ICast.CharacterID => ((IMetadata?)Character)?.ID;

    MetadataGuid ICast.ParentID => EntryID;

    string? ICast.OriginalName => null;

    string? ICast.Description => RoleNotes;

    string ICast.LanguageCode => LanguageCode ?? Language.GetString();

    IMetadata? ICast.Parent => GetEntry();

    ICharacter? ICast.Character => Character;

    ICreator? ICast.Creator => Creator;

    ISeries? ICast<ISeries>.ParentOfType => GetEntry() as ISeries;

    ISeason? ICast<ISeason>.ParentOfType => GetEntry() as ISeason;

    IEpisode? ICast<IEpisode>.ParentOfType => GetEntry() as IEpisode;

    IMovie? ICast<IMovie>.ParentOfType => GetEntry() as IMovie;

    #endregion
}
