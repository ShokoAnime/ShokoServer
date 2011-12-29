using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer
{
	public static class Constants
	{
		public static readonly string WebCacheError = @"<error>No Results</error>";
		public static readonly string AniDBTitlesURL = @"http://anidb.net/api/animetitles.dat.gz";
		public static readonly string AnonWebCacheUsername = @"AnonymousWebCacheUser";

		public const string DatabaseTypeKey = "Database";


		#region Labels
		public struct Labels
		{
			public static readonly string LASTWATCHED = "Last Watched";
			public static readonly string NEWEPISODES = "New Episodes";
			public static readonly string FAVES = "Favorites";
			public static readonly string FAVESNEW = "New in Favorites";
			public static readonly string MISSING = "Missing Episodes";
			public static readonly string MAINVIEW = "[ Main View ]";
			public static readonly string PREVIOUSFOLDER = "..";

		}

		public struct SeriesDisplayString
		{
			public static readonly string SeriesName = "<SeriesName>";
			public static readonly string AniDBNameRomaji = "<AniDBNameRomaji>";
			public static readonly string AniDBNameEnglish = "<AniDBNameEnglish>";
			public static readonly string TvDBSeason = "<TvDBSeason>";
			public static readonly string AnimeYear = "<AnimeYear>";
		}

		public struct GroupDisplayString
		{
			public static readonly string GroupName = "<GroupName>";
			public static readonly string AniDBNameRomaji = "<AniDBNameRomaji>";
			public static readonly string AniDBNameEnglish = "<AniDBNameEnglish>";
			public static readonly string AnimeYear = "<AnimeYear>";
		}

		public struct FileSelectionDisplayString
		{
			public static readonly string Group = "<AnGroup>";
			public static readonly string GroupShort = "<AnGroupShort>";
			public static readonly string FileSource = "<FileSource>";
			public static readonly string FileRes = "<FileRes>";
			public static readonly string FileCodec = "<FileCodec>";
			public static readonly string AudioCodec = "<AudioCodec>";
		}

		public struct EpisodeDisplayString
		{
			public static readonly string EpisodeNumber = "<EpNo>";
			public static readonly string EpisodeName = "<EpName>";
		}

		public struct URLS
		{
			public static readonly string MAL_Series_Prefix = @"http://myanimelist.net/anime/";

			public static readonly string AniDB_File = @"http://anidb.net/perl-bin/animedb.pl?show=file&fid={0}";
			public static readonly string AniDB_Episode = @"http://anidb.net/perl-bin/animedb.pl?show=ep&eid={0}";
			public static readonly string AniDB_Series = @"http://anidb.net/perl-bin/animedb.pl?show=anime&aid={0}";
			public static readonly string AniDB_ReleaseGroup = @"http://anidb.net/perl-bin/animedb.pl?show=group&gid={0}";
			public static readonly string AniDB_Images = @"http://img7.anidb.net/pics/anime/{0}";

			public static readonly string TvDB_Series = @"http://thetvdb.com/?tab=series&id={0}";
			//public static readonly string tvDBEpisodeURLPrefix = @"http://anidb.net/perl-bin/animedb.pl?show=ep&eid={0}";
			public static readonly string TvDB_Images = @"http://thetvdb.com/banners/{0}";

			public static readonly string MovieDB_Series = @"http://www.themoviedb.org/movie/{0}";
		}

		public struct GroupLabelStyle
		{
			public static readonly string EpCount = "Total Episode Count";
			public static readonly string Unwatched = "Only Unwatched Episode Count";
			public static readonly string WatchedUnwatched = "Watched and Unwatched Episode Counts";
		}

		public struct EpisodeLabelStyle
		{
			public static readonly string IconsDate = "Icons and Date";
			public static readonly string IconsOnly = "Icons Only";
		}

		#endregion

		public struct WebURLStrings
		{

		}

		public struct TorrentSourceNames
		{
			public static readonly string TT = "Tokyo Toshokan";
			public static readonly string AnimeSuki = "Anime Suki";
			public static readonly string BakaBT = "Baka BT";
			public static readonly string BakaUpdates = "BakaUpdates";
			public static readonly string Nyaa = "Nyaa Torrents";
		}

		public struct EpisodeTypeStrings
		{
			public static readonly string Normal = "Episodes";
			public static readonly string Credits = "Credits";
			public static readonly string Specials = "Specials";
			public static readonly string Trailer = "Trailer";
			public static readonly string Parody = "Parody";
			public static readonly string Other = "Other";
		}

		public struct TvDBURLs
		{
			public static readonly string apiKey = "B178B8940CAF4A2C";
			public static readonly string urlSeriesSearch = @"http://www.thetvdb.com/api/GetSeries.php?seriesname={0}&language=all";
			public static readonly string urlFullSeriesData = @"{0}/api/{1}/series/{2}/all/{3}.zip"; // mirrirURL, apiKey, seriesID
			public static readonly string urlBannersXML = @"{0}/api/{1}/series/{2}/banners.xml"; // mirrirURL, apiKey, seriesID
			public static readonly string urlSeriesBaseXML = @"{0}/api/{1}/series/{2}/{3}.xml"; // mirrirURL, apiKey, seriesID
			public static readonly string urlEpisodeXML = @"{0}/api/{1}/episodes/{2}/{3}.xml"; // mirrirURL, apiKey, episodeID
			public static readonly string urlLanguagesXML = @"{0}/api/{1}/languages.xml"; // mirrirURL, apiKey
			public static readonly string urlUpdatesList = @"{0}/api/Updates.php?type=all&time={1}"; // mirrirURL, server time
		}

		public struct TraktTvURLs
		{
			public static readonly string APIKey = "f9db01de75fcc4c26f26245262c7715803e376d1";
			public static readonly string URLGetShowExtended = @"http://api.trakt.tv/show/summary.json/{0}/{1}/extended"; // apiKey/ tvdb id or trakt id
			public static readonly string URLGetFriends = @"http://api.trakt.tv/user/friends.json/{0}/{1}"; // apiKey/ username
			public static readonly string URLGetActivityFriends = @"http://api.trakt.tv/activity/friends.json/{0}/episode,show/scrobble,shout"; // apiKey
			public static readonly string URLGetShowShouts = @"http://api.trakt.tv/show/shouts.json/{0}/{1}"; // apiKey / show
			public static readonly string URLGetEpisodeShouts = @"http://api.trakt.tv/show/episode/shouts.json/{0}/{1}/{2}/{3}"; // apikey/title/season/episode
			public static readonly string URLSearchShow = @"http://api.trakt.tv/search/shows.json/{0}/{1}"; // apiKey/ search criteria
			public static readonly string URLUserLibraryShowsCollection = @"http://api.trakt.tv/user/library/shows/collection.json/{0}/{1}"; // apiKey/ username
			public static readonly string URLUserLibraryShowsWatched = @"http://api.trakt.tv/user/library/shows/watched.json/{0}/{1}"; // apiKey/ username

			public static readonly string URLPostShoutShow = @"http://api.trakt.tv/shout/show/{0}"; // apiKey
			public static readonly string URLPostFriendsRequests = @"http://api.trakt.tv/friends/requests/{0}"; // apiKey
			public static readonly string URLPostFriendsDeny = @"http://api.trakt.tv/friends/deny/{0}"; // apiKey
			public static readonly string URLPostFriendsApprove = @"http://api.trakt.tv/friends/approve/{0}"; // apiKey
			public static readonly string URLPostShowScrobble = @"http://api.trakt.tv/show/scrobble/{0}"; // apiKey
			public static readonly string URLPostAccountTest = @"http://api.trakt.tv/account/test/{0}"; // apiKey
			public static readonly string URLPostAccountCreate = @"http://api.trakt.tv/account/create/{0}"; // apiKey
			public static readonly string URLPostShowEpisodeLibrary = @"http://api.trakt.tv/show/episode/library/{0}"; // apiKey
			public static readonly string URLPostShowEpisodeSeen = @"http://api.trakt.tv/show/episode/seen/{0}"; // apiKey
			public static readonly string URLPostShowEpisodeUnseen = @"http://api.trakt.tv/show/episode/unseen/{0}"; // apiKey
		}

		public struct Folders
		{
			public static readonly string thumbsSubFolder = "AnimeThumbs";
			public static readonly string thumbsTvDB = @"TvDB";
			public static readonly string thumbsAniDB = @"AniDB";
			public static readonly string thumbsAniDB_Chars = @"AniDB\Characters";
			public static readonly string thumbsAniDB_Creators = @"AniDB\Creators";
			public static readonly string thumbsMAL = @"MAL";
			public static readonly string thumbsMovieDB = @"MovieDB";
		}

		public struct AnimeTitleType
		{
			public static readonly string Main = "main";
			public static readonly string Official = "official";
			public static readonly string ShortName = "short";
			public static readonly string Synonym = "synonym";
		}

		public struct MovieDBImageSize
		{
			public static readonly string Original = "original";
			public static readonly string Thumb = "thumb";
			public static readonly string Cover = "cover";
		}

	}

	public static class Globals
	{
		public static System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.CurrentCulture;

	}
}

