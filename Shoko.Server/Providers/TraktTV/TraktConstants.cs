﻿namespace Shoko.Server.Providers.TraktTV;

public static class TraktConstants
{
    public const string ClientID = "a20707fa9666bea4acd86bc6ea2123bd6ffdbe996b4927cfdba96f4d3fca3042";
    public const string ClientSecret = "7ef5eec766070fa0b34a4a4a2fea2ad0ddbe9bb1bc1e8eb621551c52fb288739";
    public const string BaseAPIURL = @"https://api.trakt.tv";
    public const string BaseWebsiteURL = @"https://trakt.tv";
}

public enum TraktSyncType
{
    CollectionAdd = 1,
    CollectionRemove = 2,
    HistoryAdd = 3,
    HistoryRemove = 4
}

public enum ScrobblePlayingStatus
{
    Start = 1,
    Pause = 2,
    Stop = 3
}

public enum ScrobblePlayingType
{
    movie = 1,
    episode = 2
}

public static class TraktStatusCodes
{
    // http://docs.trakt.apiary.io/#introduction/status-codes
    // Status Codes

    public const int Success = 200;
    public const int Success_Post = 201;
    public const int Success_Delete = 204;
    public const int Awaiting_Auth = 400;
    public const int Token_Expired = 410;

    public const int Bad_Request = 400;
    public const int Unauthorized = 401;
    public const int Forbidden = 403;

    public const int Not_Found = 404;
    public const int Method_Not_Found = 405;
    public const int Conflict = 409;

    public const int Precondition_Failed = 412;
    public const int Unprocessable_Entity = 422;
    public const int Rate_Limit_Exceeded = 429;

    public const int Server_Error = 500;
    public const int Service_Unavailable = 503;
    public const int Service_Unavailable2 = 520;
    public const int Service_Unavailable3 = 521;
    public const int Service_Unavailable4 = 522;
}
