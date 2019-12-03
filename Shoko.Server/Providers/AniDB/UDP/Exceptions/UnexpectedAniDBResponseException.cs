using System;

namespace Shoko.Server.Providers.AniDB.UDP.Exceptions
{
    [Serializable]
    public class UnexpectedAniDBResponseException : Exception
    {
        public string Message { get; set; }
        public string Response { get; set; }
        public AniDBUDPReturnCode ReturnCode { get; set; }

        public UnexpectedAniDBResponseException()
        {
            
        }

        public UnexpectedAniDBResponseException(AniDBUDPReturnCode code, string response) : base(
            $"Unexpected AniDB Response: {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
        }
        
        public UnexpectedAniDBResponseException(string message, AniDBUDPReturnCode code, string response) : base(
            $"Unexpected AniDB Response: {message} -- {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
            Message = message;
        }
    }
}