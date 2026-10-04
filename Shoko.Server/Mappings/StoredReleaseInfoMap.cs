using FluentNHibernate.Mapping;
using Shoko.Abstractions.Video.Enums;
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

        Map(x => x.ED2K).CustomType<PooledStringType>().Not.Nullable();
        Map(x => x.FileSize).Not.Nullable();
        Map(x => x.ID);
        Map(x => x.ProviderName).CustomType<PooledStringType>().Not.Nullable();
        Map(x => x.ReleaseURI);
        Map(x => x.Version).Not.Nullable();
        Map(x => x.ProvidedFileSize);
        Map(x => x.Comment);
        Map(x => x.OriginalFilename);
        Map(x => x.IsCensored);
        Map(x => x.IsChaptered);
        Map(x => x.IsCreditless);
        Map(x => x.IsCorrupted).Not.Nullable();
        Map(x => x.IsDeprecated).Not.Nullable();
        Map(x => x.Source).CustomType<ReleaseSource>().Not.Nullable();
        Map(x => x.GroupID).CustomType<PooledStringType>();
        Map(x => x.GroupSource).CustomType<PooledStringType>();
        Map(x => x.GroupName).CustomType<PooledStringType>();
        Map(x => x.GroupShortName).CustomType<PooledStringType>();
        Map(x => x.EmbeddedHashes).Column("Hashes");
        Map(x => x.EmbeddedAudioLanguages).CustomType<PooledStringType>().Column("AudioLanguages");
        Map(x => x.EmbeddedSubtitleLanguages).CustomType<PooledStringType>().Column("SubtitleLanguages");
        Map(x => x.EmbeddedCrossReferences).Column("CrossReferences").Not.Nullable();
        Map(x => x.ReleasedAt).CustomType<DateOnlyConverter>();
        Map(x => x.LastUpdatedAt).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.IsPublic).Nullable();
        Map(x => x.PreventRescan).Not.Nullable();
        Map(x => x.DeferToNext).Not.Nullable();
    }
}
