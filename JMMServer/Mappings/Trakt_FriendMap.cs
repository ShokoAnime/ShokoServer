using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings 
{
	public class Trakt_FriendMap : ClassMap<Trakt_Friend>
	{
		public Trakt_FriendMap()
        {
			Not.LazyLoad();
            Id(x => x.Trakt_FriendID);

			Map(x => x.About);
			Map(x => x.Age);
			Map(x => x.Avatar);
			Map(x => x.FullName);
			Map(x => x.Gender);
			Map(x => x.Joined).Not.Nullable();
			Map(x => x.LastAvatarUpdate).Not.Nullable();
			Map(x => x.Location);
			Map(x => x.Url);
			Map(x => x.Username);
        }
	}
}
