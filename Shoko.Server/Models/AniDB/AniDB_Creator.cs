using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;

using AbstractCreatorType = Shoko.Plugin.Abstractions.Enums.CreatorType;
using DataSourceEnum = Shoko.Plugin.Abstractions.Enums.DataSourceEnum;

#nullable enable
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

    public IReadOnlyList<AniDB_Anime_Character_Creator> Characters
        => RepoFactory.AniDB_Anime_Character_Creator.GetByCreatorID(CreatorID);

    public IReadOnlyList<AniDB_Anime_Staff> Staff
        => RepoFactory.AniDB_Anime_Staff.GetByCreatorID(CreatorID);

    #region IMetadata Implementation

    int IMetadata<int>.ID => CreatorID;

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    #endregion

    #region IWithDescriptions Implementation

    string IWithDescriptions.DefaultDescription => string.Empty;

    string IWithDescriptions.PreferredDescription => string.Empty;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions => [];

    #endregion

    #region IWithPortraitImage Implementation

    IImageMetadata? IWithPortraitImage.PortraitImage => this.GetImageMetadata();

    #endregion

    #region ICreator Implementation

    AbstractCreatorType ICreator.Type => Type switch
    {
        CreatorType.Person => AbstractCreatorType.Person,
        CreatorType.Company => AbstractCreatorType.Company,
        CreatorType.Collaboration => AbstractCreatorType.Collaboration,
        CreatorType.Other => AbstractCreatorType.Other,
        _ => AbstractCreatorType.Unknown,
    };

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
