using System;

namespace Shoko.Server.Providers.AniDB.MyList.Exceptions
{
    [Serializable]
    public class UnexpectedAniDBResponse : Exception
    {
        public string Message { get; set; }
        public string Response { get; set; }
        public AniDBUDPReturnCode ReturnCode { get; set; }

        public UnexpectedAniDBResponse()
        {
            
        }

        public UnexpectedAniDBResponse(AniDBUDPReturnCode code, string response) : base(
            $"Unexpected AniDB Response: {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
        }
        
        public UnexpectedAniDBResponse(string message, AniDBUDPReturnCode code, string response) : base(
            $"Unexpected AniDB Response: {message} -- {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
            Message = message;
        }
    }
}