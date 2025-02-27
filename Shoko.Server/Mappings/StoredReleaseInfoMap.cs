using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Release;

namespace Shoko.Server.Mappings;

public class StoredReleaseInfoMap : ClassMap<StoredReleaseInfo>
{
    public StoredReleaseInfoMap()
    {
        Table("StoredReleaseInfo");

        Not.LazyLoad();
        Id(x => x.StoredReleaseInfoID);

        Map(x => x.ED2K).Not.Nullable();
        Map(x => x.FileSize).Not.Nullable();
        Map(x => x.ID);
        Map(x => x.ProviderName).Not.Nullable();
        Map(x => x.ReleaseURI);
        Map(x => x.Revision).Not.Nullable();
        Map(x => x.ProvidedFileSize);
        Map(x => x.Comment);
        Map(x => x.OriginalFilename);
        Map(x => x.IsCensored);
        Map(x => x.IsCorrupted).Not.Nullable();
        Map(x => x.Source).Not.Nullable();
        Map(x => x.GroupID);
        Map(x => x.GroupSource);
        Map(x => x.GroupName);
        Map(x => x.GroupShortName);
        Map(x => x.Hashes).CustomType<TypelessMessagePackConverter>();
        Map(x => x.EmbeddedAudioLanguages).Column("AudioLanguages");
        Map(x => x.EmbeddedSubtitleLanguages).Column("SubtitleLanguages");
        Map(x => x.EmbeddedCrossReferences).Column("CrossReferences").Not.Nullable();
        Map(x => x.ReleasedAt).CustomType<DateOnlyConverter>();
        Map(x => x.LastUpdatedAt).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
    }
}
