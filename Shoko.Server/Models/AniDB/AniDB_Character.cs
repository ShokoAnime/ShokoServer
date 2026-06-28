using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Character : ICharacter
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

    #endregion

    #region IMetadata Implementation

    DataEntityType IMetadata.EntityType => DataEntityType.Character;

    int IMetadata<int>.ID => CharacterID;

    DataSource IMetadata.Source => DataSource.AniDB;

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdated;

    #endregion

    #region IWithDescriptions Implementation

    IText? IWithDescriptions.DefaultDescription => Description is { Length: > 0 }
        ? new TextStub
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        }
        : null;

    IText? IWithDescriptions.PreferredDescription => Description is { Length: > 0 } && ISettingsProvider.Instance.GetSettings().Language.DescriptionLanguageOrder.Contains("en")
        ? new TextStub
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        }
        : null;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => [
        new TextStub
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = Description,
            Source = DataSource.AniDB,
        },
    ];

    #endregion

    #region IWithImages Implementation

    public IImage? GetPreferredImageForType(ImageEntityType imageType)
        => GetImages(imageType: imageType).FirstOrDefault(image => image.IsPreferred);

    public IImageCrossReference? GetPreferredImageCrossReferenceForType(ImageEntityType imageType)
        => GetImageCrossReferences(imageType: imageType).FirstOrDefault(xref => xref.IsPreferred);

    public IReadOnlyList<IImage> GetImages(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null, bool? isAvailable = null, bool primaryImage = false, bool? linkedEntityImages = null)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImagesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, isAvailable: isAvailable, primaryImage: primaryImage, linkedEntityImages: linkedEntityImages);

    public IReadOnlyList<IImageCrossReference> GetImageCrossReferences(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null, bool? isAvailable = null, bool? primaryImage = null, bool? linkedEntityImages = null)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImageCrossReferencesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, isAvailable, primaryImage, linkedEntityImages);

    #endregion

    #region IWithPrimaryImage Implementation

    public IImage? DefaultPrimaryImage => DefaultPrimaryImageCrossReference is { } xref && xref.GetImage() is { } image
        ? new ImageStub(image, xref)
        : null;

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(ImagePath) && IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniDB, ImagePath) is { } imageID
        ? GetImageCrossReferences(imageType: ImageEntityType.Primary).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region ICharacter Implementation

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
