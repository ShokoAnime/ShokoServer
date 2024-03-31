using System;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Update a file in the MyList
/// </summary>
public class RequestUpdateFileID : UDPRequest<Void>
{
    protected override string BaseCommand
    {
        get
        {
            var command = $"MYLISTADD fid={FileID}&state={(int)State}";
            switch (IsWatched)
            {
                case true:
                    var date = WatchedDate ?? DateTime.Now;
                    command += $"&viewed=1&viewdate={Commons.Utils.AniDB.GetAniDBDateAsSeconds(date)}";    
                    break;
                case false:
                    command += "&viewed=0";    
                    break;
            }

            command += "&edit=1";

            return command;
        }
    }

    public int FileID { get; set; }

    public MyList_State State { get; set; }

    public bool? IsWatched { get; set; }
    public DateTime? WatchedDate { get; set; }

    protected override UDPResponse<Void> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        switch (code)
        {
            case UDPReturnCode.MYLIST_ENTRY_EDITED:
            case UDPReturnCode.NO_SUCH_MYLIST_ENTRY:
            case UDPReturnCode.NO_SUCH_FILE:
                return new UDPResponse<Void> { Code = code };
        }

        throw new UnexpectedUDPResponseException(code, receivedData, Command);
    }

    public RequestUpdateFileID(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
