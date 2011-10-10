using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AniDBAPI
{
	public enum AniDBFileStatus 
	{ 
		Unknown = 1, 
		HDD = 2,
		DVD = 3 
	}

	public enum enAniDBVoteType
	{
		Anime = 1,
		AnimeTemp = 2,
		Group = 3,
		Episode = 4
	}

	public enum enAniDBCommandType
	{
		Login = 1,
		Logout = 2,
		AddFile = 3,
		GetFileInfo = 4,
		GetAnimeInfo = 5,
		GetEpisodeInfo = 6,
		GetAnimeDescription = 7,
		GetMyListFileInfo = 8,
		GetGroupStatus = 9,
		UpdateFile = 10,
		GetCharInfo = 11,
		GetCreatorInfo = 12,
		GetCalendar = 13,
		GetReview = 14,
		AddVote = 15,
		GetAnimeInfoHTTP = 16,
		GetNotifyList = 17,
		GetNotifyGet = 18,
		GetUpdated = 19,
		GetMyListHTTP = 20,
		Ping = 21,
		GetGroup = 22,
		GetVotesHTTP = 23,
		DeleteFile = 24

	}

	public enum enHelperActivityType
	{
		//Login
		LoggingIn = 1,
		LoggedIn = 2,
		LoggedInAlready = 3,
		LoggedOut = 4,
		LoginFailed = 5,
		LoggingOut = 6,
		LoginRequired = 7,
		//File
		HashingFile = 10,
		HashComplete = 11,
		AddingFile = 12,
		FileAdded = 13,
		GettingFileInfo = 14,
		GotFileInfo = 15,
		FileDoesNotExist = 16,
		FileAlreadyExists = 17,
		NoSuchFile = 18,
		UpdatingFile = 20,
		DeletingFile = 21,
		FileDeleted = 22,
		//Episode
		GotEpisodeInfo = 30,
		GettingEpisodeInfo = 32,
		NoSuchEpisode = 33,
		//Anime
		GettingAnimeInfo = 41,
		GotAnimeInfo = 42,
		NoSuchAnime = 43,
		GettingAnimeDesc = 44,
		GotAnimeDesc = 45,
		GettingGroupStatus = 46,
		GotGroupStatus = 47,
		NoGroupsFound = 48,
		//My List
		GettingMyListFileInfo = 50,
		NoSuchMyListFile = 51,
		GotMyListFileInfo = 52,
		//Char
		GotCharInfo = 60,
		GettingCharInfo = 61,
		NoSuchChar = 62,
		//Creator
		GotCreatorInfo = 70,
		GettingCreatorInfo = 71,
		NoSuchCreator = 72,
		// Calendar
		GotCalendar = 81,
		CalendarEmpty = 82,
		GettingCalendar = 83,
		// Review
		GotReview = 91,
		GettingReview = 92,
		NoSuchReview = 93,
		// Vote
		Voted = 111,
		AddingVote = 112,
		VoteFound = 113,
		VoteUpdated = 114,
		VoteRevoked = 115,
		NoSuchVote = 116,
		InvalidVoteType = 117,
		InvalidVoteValue = 118,
		PermVoteNotAllowed = 119,
		PermVoteAlready = 120,
		//Misc
		StatusUpdate = 40,
		OtherError = 100,
		// HTTP,
		GettingAnimeHTTP = 121,
		GotAnimeInfoHTTP = 122,
		GettingMyListHTTP = 123,
		GotMyListHTTP = 124,
		GettingVotesHTTP = 125,
		GotVotesHTTP = 126,
		// Notify List
		GotNotifyList = 130,
		GettingNotifyList = 131,
		// Notify Get
		GotNotifyGet = 140,
		GettingNotifyGet = 141,
		NoSuchNotify = 142,
		// Updated
		GotUpdated = 151,
		GettingUpdated = 152,
		NoUpdates = 153,
		// Group
		GettingGroup = 160,
		GotGroup = 161,
		NoSuchGroup = 162,

		Ping = 900,
		PingFailed = 901,
		PingPong = 902,

		UnknownCommand = 950,
		Banned = 951
	}

	
}
