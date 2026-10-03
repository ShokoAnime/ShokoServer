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
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.AniDB;

public class AniDB_Character : ICharacter, IInlineTextSource
{
    #region Server DB columns

    public int AniDB_CharacterID { get; set; }

    public int CharacterID { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ImagePath { get; set; } = string.Empty;

    public PersonGender Gender { get; set; }

    public CharacterType Type { get; set; }

    public DateTime LastUpdated { get; set; }

    /// <summary>
    ///   When the character was first stored, in local time. Set once, on the
    ///   first save.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    #endregion

    #region IMetadata Implementation

    MetadataGuid IMetadata.ID => new(MetadataSource.AniDB, MetadataEntityType.Character, CharacterID.ToString());

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => CreatedAt.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdated;

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => null;

    IText? IInlineTextSource.InlineOverview => InlineText.Overview(MetadataSource.AniDB, Description, TitleLanguage.English, "en");

    #endregion

    #region IWithOverviews Implementation

    IText? IWithOverviews.DefaultOverview => TextAccess.Manager.DefaultOverviewFor(this);

    IText? IWithOverviews.PreferredOverview => TextAccess.Manager.PreferredOverviewFor(this);

    // A missing description is still listed, empty, as it always was.
    IReadOnlyList<IText> IWithOverviews.Overviews => [
        ((IInlineTextSource)this).InlineOverview ?? new TextStub
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = MetadataSource.AniDB,
        },
    ];

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(ImagePath) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.AniDB, ImagePath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = MetadataSource.AniDB, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region IWithResources Implementation

    IReadOnlyList<Resource> IWithResources.Resources
        => [.. ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this)];

    #endregion

    #region ICharacter Implementation

    // Characters come with their anime, and nothing stores when one was last fetched.
    DateTime? ICharacter.LastRefreshedAt => null;

    IReadOnlyList<ITitle> ICharacter.AlternativeNames => [];

    FuzzyDateOnly? ICharacter.BirthDay => null;

    IEnumerable<ICast<IEpisode>> ICharacter.EpisodeCastRoles =>
        RepoFactory.AniDB_Anime_Character.GetByCharacterID(CharacterID)
        .GroupBy(xref => xref.AnimeID)
        .OrderBy(x => x.Key)
        .SelectMany(GetCastForGrouping);

    IEnumerable<ICast<IEpisode>> GetCastForGrouping(IGrouping<int, AniDB_Anime_Character> groupBy)
    {
        var xrefs = groupBy
            .SelectMany(x => x.CreatorCrossReferences is { Count: > 0 } xref
                ? xref.Select(xref => (xref: x, xref.CreatorID, xref.Ordering))
                : [(xref: x, 0, 0)]
            )
            .OrderBy(obj => obj.xref.Ordering)
            .ToList();
        var episodes = RepoFactory.AniDB_Episode.GetByAnimeID(groupBy.Key);
        foreach (var episode in episodes)
            foreach (var (xref, creatorID, _) in xrefs)
                yield return new AniDB_Cast<IEpisode>(xref, this, creatorID, () => episode);
    }

    IEnumerable<ICast<IMovie>> ICharacter.MovieCastRoles => [];

    IEnumerable<ICast<ISeries>> ICharacter.SeriesCastRoles =>
        RepoFactory.AniDB_Anime_Character.GetByCharacterID(CharacterID)
            .SelectMany(x => x.CreatorCrossReferences is { Count: > 0 } xref
                ? xref.Select(xref => new AniDB_Cast<ISeries>(x, this, xref.CreatorID, () => x.Anime))
                : [new AniDB_Cast<ISeries>(x, this, null, () => x.Anime)]
            )
            .OrderBy(x => x.ParentID)
            .ThenBy(x => x.Ordering)
            .ToList();

    #endregion
}
