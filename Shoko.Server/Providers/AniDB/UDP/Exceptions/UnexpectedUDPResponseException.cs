using System;
using System.Text.RegularExpressions;
// ReSharper disable VirtualMemberCallInConstructor

namespace Shoko.Server.Providers.AniDB.UDP.Exceptions;

[Serializable]
public class UnexpectedUDPResponseException : Exception
{
    public string Request { get; set; }
    public string Response { get; set; }
    public UDPReturnCode ReturnCode { get; set; }

    public UnexpectedUDPResponseException(string response, string request) : this(UDPReturnCode.UNKNOWN_COMMAND, response, request) {}
    public UnexpectedUDPResponseException(UDPReturnCode code, string response, string request) : base(
        $"Unexpected AniDB Response: {code} | {response} | {RemoveUserInfo(request)}")
    {
        Request = request;
        Response = response;
        ReturnCode = code;
        Data[nameof(Request)] = RemoveUserInfo(request);
        Data[nameof(Response)] = response;
        Data[nameof(ReturnCode)] = code;
    }

    public UnexpectedUDPResponseException(string message, UDPReturnCode code, string response, string request) : base(RemoveUserInfo(message))
    {
        Request = request;
        Response = response;
        ReturnCode = code;
        Data[nameof(Request)] = RemoveUserInfo(request);
        Data[nameof(Response)] = response;
        Data[nameof(ReturnCode)] = code;
    }

    private static readonly Regex s_regex = new(@"(user|pass|s)=\w+", RegexOptions.Compiled);
    private static string RemoveUserInfo(string request) => request == null ? string.Empty : s_regex.Replace(request, "$1=****");
}
