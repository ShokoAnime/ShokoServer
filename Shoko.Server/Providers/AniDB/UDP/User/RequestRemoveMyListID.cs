using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Remove a file from MyList.
/// </summary>
public class RequestRemoveMyListID : UDPRequest<Void>
{
    // These are dependent on context
    protected override string BaseCommand => $"MYLISTDEL lid={MyListID}";

    public int MyListID { get; set; }

    protected override UDPResponse<Void> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        switch (code)
        {
            case UDPReturnCode.MYLIST_ENTRY_DELETED:
            case UDPReturnCode.NO_SUCH_MYLIST_ENTRY:
                return new UDPResponse<Void> { Code = code };
        }

        throw new UnexpectedUDPResponseException(code, receivedData);
    }

    public RequestRemoveMyListID(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory,
        handler)
    {
    }
}
