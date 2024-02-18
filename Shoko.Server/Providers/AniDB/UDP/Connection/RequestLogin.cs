using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Connection;

public class RequestLogin : UDPRequest<ResponseLogin>
{
    public string Username { get; set; }
    public string Password { get; set; }
    public bool UseUnicode { get; set; }

    protected override string BaseCommand =>
        $"AUTH user={Username}&pass={Password}&protover=3&client=ommserver&clientver=2&comp=1&imgserver=1&enc=utf-16";

    protected override UDPResponse<ResponseLogin> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        var i = receivedData.IndexOf("LOGIN", StringComparison.Ordinal);
        if (i < 0)
        {
            throw new UnexpectedUDPResponseException(code, receivedData);
        }

        // after response code, before "LOGIN"
        var sessionID = receivedData.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sessionID))
        {
            throw new UnexpectedUDPResponseException(code, receivedData);
        }

        var imageServer = receivedData.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return new UDPResponse<ResponseLogin>
        {
            Response = new ResponseLogin { SessionID = sessionID, ImageServer = imageServer }, Code = code
        };
    }

    protected override void PreExecute(string sessionID)
    {
        // Override to prevent attaching our non-existent sessionID
    }

    public override UDPResponse<ResponseLogin> Send()
    {
        Command = BaseCommand;
        // LOGIN commands have special needs, so we want to handle this differently
        var rawResponse = Handler.CallAniDBUDPDirectly(Command, UseUnicode).Result;
        var response = ParseResponse(rawResponse, true);
        var parsedResponse = ParseResponse(response);
        return parsedResponse;
    }

    public RequestLogin(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
