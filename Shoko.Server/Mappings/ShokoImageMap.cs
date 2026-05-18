using FluentNHibernate.Mapping;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Mappings;

public class ShokoImageMap : ClassMap<ShokoImage>
{
    public ShokoImageMap()
    {
        Table("ShokoImage");

        Not.LazyLoad();
        Id(x => x.ID).GeneratedBy.Assigned();

        Map(x => x.LocalID).Not.Nullable();
        Map(x => x.PrimaryID).Not.Nullable();
        Map(x => x.Source).Not.Nullable();
        Map(x => x.ResourceID).Not.Nullable();
        Map(x => x.LanguageCode).Nullable();
        Map(x => x.CountryCode).Nullable();
        Map(x => x.Width).Nullable();
        Map(x => x.Height).Nullable();
        Map(x => x.ContentType).Not.Nullable();
        Map(x => x.DownloadAttempts).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
