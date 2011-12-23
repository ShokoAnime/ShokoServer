using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using JMMServer.ImageDownload;
using JMMServer.Providers.TraktTV;

namespace JMMServer.Entities
{
	public class Trakt_Friend
	{
		public int Trakt_FriendID { get; private set; }
		public string Username { get; set; }
		public string FullName { get; set; }
		public string Gender { get; set; }
		public string Age { get; set; }
		public string Location { get; set; }
		public string About { get; set; }
		public int Joined { get; set; }
		public string Avatar { get; set; }
		public string Url { get; set; }
		public DateTime LastAvatarUpdate { get; set; }

		public string FullImagePath
		{
			get
			{
				// typical url
				// http://vicmackey.trakt.tv/images/avatars/837.jpg
				// http://gravatar.com/avatar/f894a4cbd5e8bcbb1a79010699af1183.jpg?s=140&r=pg&d=http%3A%2F%2Fvicmackey.trakt.tv%2Fimages%2Favatar-large.jpg

				if (string.IsNullOrEmpty(Avatar)) return "";

				string path = ImageUtils.GetTraktImagePath_Avatars();
				return Path.Combine(path, string.Format("{0}.jpg", Username));
			}
		}

		public void Populate(TraktTVUser user)
		{
			Username = user.username;
			FullName = user.full_name;
			Gender = user.gender;
			Age = user.age;
			Location = user.location;
			About = user.about;
			Joined = user.joined;
			Avatar = user.avatar;
			Url = user.url;
		}

		public void Populate(TraktTV_UserActivity user)
		{
			Username = user.username;
			FullName = user.full_name;
			Gender = user.gender;
			Age = user.age;
			Location = user.location;
			About = user.about;
			Joined = user.joined;
			Avatar = user.avatar;
			Url = user.url;
		}
	}
}
