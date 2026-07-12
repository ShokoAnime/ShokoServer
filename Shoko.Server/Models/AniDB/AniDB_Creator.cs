using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Repositories;

using AbstractCreatorType = Shoko.Abstractions.Metadata.Enums.CreatorType;
using CreatorType = Shoko.Server.Providers.AniDB.CreatorType;
using DataSource = Shoko.Abstractions.Metadata.Enums.DataSource;

#pragma warning disable CS0618
namespace Shoko.Server.Models.AniDB;

public class AniDB_Creator : ICreator
{
    #region DB Columns

    /// <summary>
    /// The local ID of the creator.
    /// </summary>
    public int AniDB_CreatorID { get; set; }

    /// <summary>
    /// The global ID of the creator.
    /// </summary>
    public int CreatorID { get; set; }

    /// <summary>
    /// The name of the creator, transcribed to use the latin alphabet.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The original name of the creator.
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    /// The type of creator.
    /// </summary>
    public CreatorType Type { get; set; }

    /// <summary>
    /// The location of the image associated with the creator.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// The URL of the creator's English homepage.
    /// </summary>
    public string? EnglishHomepageUrl { get; set; }

    /// <summary>
    /// The URL of the creator's Japanese homepage.
    /// </summary>
    public string? JapaneseHomepageUrl { get; set; }

    /// <summary>
    /// The URL of the creator's English Wikipedia page.
    /// </summary>
    public string? EnglishWikiUrl { get; set; }

    /// <summary>
    /// The URL of the creator's Japanese Wikipedia page.
    /// </summary>
    public string? JapaneseWikiUrl { get; set; }

    /// <summary>
    /// The date that the creator was last updated on AniDB.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    public AbstractCreatorType AbstractType => Type switch
    {
        CreatorType.Person => AbstractCreatorType.Person,
        CreatorType.Company => AbstractCreatorType.Company,
        CreatorType.Collaboration => AbstractCreatorType.Collaboration,
        CreatorType.Other => AbstractCreatorType.Other,
        _ => AbstractCreatorType.Unknown,
    };

    public IReadOnlyList<AniDB_Anime_Character_Creator> Characters
        => RepoFactory.AniDB_Anime_Character_Creator.GetByCreatorID(CreatorID);

    public IReadOnlyList<AniDB_Anime_Staff> Staff
        => RepoFactory.AniDB_Anime_Staff.GetByCreatorID(CreatorID);

    /// <summary>
    ///   External resources/links associated with the creator.
    /// </summary>
    public IReadOnlyList<Resource> Resources
    {
        get
        {
            var list = new List<Resource>();
            if (!string.IsNullOrEmpty(EnglishHomepageUrl))
                list.Add(new() { Type = ResourceType.Website, Name = "Homepage (EN)", Url = EnglishHomepageUrl, LanguageCode = "en" });
            if (!string.IsNullOrEmpty(JapaneseHomepageUrl))
                list.Add(new() { Type = ResourceType.Website, Name = "Homepage (JP)", Url = JapaneseHomepageUrl, LanguageCode = "ja" });
            if (!string.IsNullOrEmpty(EnglishWikiUrl))
                list.Add(new() { Type = ResourceType.Metadata, Name = "Wikipedia (EN)", Url = EnglishWikiUrl, LanguageCode = "en" });
            if (!string.IsNullOrEmpty(JapaneseWikiUrl))
                list.Add(new() { Type = ResourceType.Metadata, Name = "Wikipedia (JP)", Url = JapaneseWikiUrl, LanguageCode = "ja" });
            list.AddRange(ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this));
            return list;
        }
    }

    #region IMetadata Implementation

    DataEntityType IMetadata.EntityType => DataEntityType.Creator;

    int IMetadata<int>.ID => CreatorID;

    DataSource IMetadata.Source => DataSource.AniDB;

    #endregion

    #region IWithDescriptions Implementation

    IText? IWithDescriptions.DefaultDescription => null;

    IText? IWithDescriptions.PreferredDescription => null;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => [];

    #endregion

    #region IWithImages Implementation

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(ImagePath) && IImageManager.GetIDForImageSourceAndResourceID(DataSource.AniDB, ImagePath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = DataSource.AniDB, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region ICreator Implementation

    AbstractCreatorType ICreator.Type => AbstractType;

    DateOnly? ICreator.BirthDay => null;

    IEnumerable<ICast<IEpisode>> ICreator.EpisodeCastRoles =>
        RepoFactory.AniDB_Anime_Character_Creator.GetByCreatorID(CreatorID)
        .GroupBy(xref => xref.AnimeID)
        .OrderBy(x => x.Key)
        .SelectMany(GetCastForGrouping);

    IEnumerable<ICast<IEpisode>> GetCastForGrouping(IGrouping<int, AniDB_Anime_Character_Creator> groupBy)
    {
        var xrefs = groupBy
            .GroupBy(xref => xref.CharacterID)
            .DistinctBy(xref => xref.Key)
            .Select(x => x.First().CharacterCrossReference is { } xref && xref.Character is { } character
                ? new { xref, character }
                : null
            )
            .WhereNotNull()
            .OrderBy(obj => obj.xref.Ordering)
            .ToList();
        var episodes = RepoFactory.AniDB_Episode.GetByAnimeID(groupBy.Key);
        foreach (var episode in episodes)
            foreach (var obj in xrefs)
                yield return new AniDB_Cast<IEpisode>(obj.xref, obj.character, CreatorID, () => episode);
    }

    IEnumerable<ICast<IMovie>> ICreator.MovieCastRoles => [];

    IEnumerable<ICast<ISeries>> ICreator.SeriesCastRoles =>
        RepoFactory.AniDB_Anime_Character_Creator.GetByCreatorID(CreatorID)
            .Select(x => x.CharacterCrossReference is { } xref && xref.Character is { } character
                ? new AniDB_Cast<ISeries>(xref, character, CreatorID, () => xref.Anime)
                : null
            )
            .WhereNotNull()
            .OrderBy(x => x.ParentID)
            .ThenBy(x => x.Ordering)
            .ToList();

    IEnumerable<ICrew<IEpisode>> ICreator.EpisodeCrewRoles => RepoFactory.AniDB_Anime_Staff.GetByCreatorID(CreatorID)
        .GroupBy(xref => xref.AnimeID)
        .OrderBy(x => x.Key)
        .SelectMany(GetCrewForGrouping);

    IEnumerable<ICrew<IEpisode>> GetCrewForGrouping(IGrouping<int, AniDB_Anime_Staff> groupBy)
    {
        var episodes = RepoFactory.AniDB_Episode.GetByAnimeID(groupBy.Key);
        foreach (var episode in episodes)
            foreach (var xref in groupBy.OrderBy(x => x.Ordering))
                yield return new AniDB_Crew<IEpisode>(xref, () => episode);
    }

    IEnumerable<ICrew<IMovie>> ICreator.MovieCrewRoles => [];

    IEnumerable<ICrew<ISeries>> ICreator.SeriesCrewRoles => RepoFactory.AniDB_Anime_Staff.GetByCreatorID(CreatorID)
        .Select(xref => new AniDB_Crew<ISeries>(xref, () => xref.Anime))
        .OrderBy(x => x.ParentID)
        .ThenBy(x => x.Ordering)
        .ToList();

    #endregion
}
