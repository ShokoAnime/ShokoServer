using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Similar : IAnidbSuggestion
{
    public int AniDB_Anime_SimilarID { get; set; }

    public int AnimeID { get; set; }

    public int SimilarAnimeID { get; set; }

    public int Approval { get; set; }

    public int Total { get; set; }

    /// <summary>
    /// The entry's position in AniDB's own list, best first, starting at
    /// <c>0</c>.
    /// </summary>
    public int Ordering { get; set; }

    #region IAnidbSuggestion Implementation

    IAnidbAnime? ISuggestedMetadata<IAnidbAnime, IAnidbAnime>.Base => RepoFactory.AniDB_Anime.GetByID(AnimeID);

    IAnidbAnime? ISuggestedMetadata<IAnidbAnime, IAnidbAnime>.Suggested => RepoFactory.AniDB_Anime.GetByID(SimilarAnimeID);

    int IAnidbSuggestion.ApprovalVotes => Approval;

    int IAnidbSuggestion.TotalVotes => Total;

    #endregion

    #region ISuggestedMetadata Implementation

    MetadataGuid ISuggestedMetadata.BaseID => new(MetadataSource.AniDB, MetadataEntityType.Series, AnimeID.ToString());

    MetadataGuid ISuggestedMetadata.SuggestedID => new(MetadataSource.AniDB, MetadataEntityType.Series, SimilarAnimeID.ToString());

    IMetadata? ISuggestedMetadata.Base => RepoFactory.AniDB_Anime.GetByID(AnimeID);

    IMetadata? ISuggestedMetadata.Suggested => RepoFactory.AniDB_Anime.GetByID(SimilarAnimeID);

    // AniDB's similar anime are user-voted similarities, not "watch this next"
    // suggestions.
    SuggestionKind ISuggestedMetadata.Kind => SuggestionKind.Similar;

    int? ISuggestedMetadata.Order => Ordering;

    double? ISuggestedMetadata.ApprovalRating => Total is 0 ? null : Approval / (double)Total * 100;

    int? ISuggestedMetadata.Votes => Total;

    MetadataSource ISuggestedMetadata.Source => MetadataSource.AniDB;

    public bool Equals(ISuggestedMetadata? other)
        => other is not null &&
            other.Source == MetadataSource.AniDB &&
            other.BaseID == ((ISuggestedMetadata)this).BaseID &&
            other.SuggestedID == ((ISuggestedMetadata)this).SuggestedID;

    #endregion

}
