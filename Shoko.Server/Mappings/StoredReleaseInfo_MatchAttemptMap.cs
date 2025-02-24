using FluentNHibernate.Mapping;
using Shoko.Server.Models.Release;

namespace Shoko.Server.Mappings;

public class StoredReleaseInfo_MatchAttemptMap : ClassMap<StoredReleaseInfo_MatchAttempt>
{
    public StoredReleaseInfo_MatchAttemptMap()
    {
        Table("StoredReleaseInfo_MatchAttempt");

        Not.LazyLoad();
        Id(x => x.StoredReleaseInfo_MatchAttemptID);

        Map(x => x.ProviderName);
        Map(x => x.ED2K).Not.Nullable();
        Map(x => x.FileSize).Not.Nullable();
        Map(x => x.EmbeddedAttemptProviderNames).Column("AttemptProviderNames").Not.Nullable();
        Map(x => x.AttemptStartedAt).Not.Nullable();
        Map(x => x.AttemptEndedAt).Not.Nullable();
    }
}
