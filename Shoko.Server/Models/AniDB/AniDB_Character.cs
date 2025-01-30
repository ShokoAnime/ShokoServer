using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

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

    int IMetadata<int>.ID => CharacterID;

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    #endregion

    #region IWithDescriptions Implementation

    string IWithDescriptions.DefaultDescription => Description;

    string IWithDescriptions.PreferredDescription => Description;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions => string.IsNullOrEmpty(Description)
        ? [] : [new() { Value = Description, CountryCode = "US", LanguageCode = "en", Source = DataSourceEnum.AniDB }];

    #endregion

    #region IWithPortraitImage Implementation

    IImageMetadata? IWithPortraitImage.PortraitImage => this.GetImageMetadata();

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
