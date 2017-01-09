using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class PlaylistMap : ClassMap<Playlist>
    {
        public PlaylistMap()
        {
            Not.LazyLoad();
            Id(x => x.PlaylistID);

            Map(x => x.PlaylistName);
            Map(x => x.PlaylistItems);
            Map(x => x.DefaultPlayOrder).Not.Nullable();
            Map(x => x.PlayWatched).Not.Nullable();
            Map(x => x.PlayUnwatched).Not.Nullable();
        }
    }
}