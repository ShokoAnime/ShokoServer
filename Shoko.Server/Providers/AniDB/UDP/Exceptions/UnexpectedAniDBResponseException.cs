using System;

namespace Shoko.Server.Providers.AniDB.UDP.Exceptions
{
    [Serializable]
    public class UnexpectedAniDBResponseException : Exception
    {
        public string Message { get; set; }
        public string Response { get; set; }
        public UDPReturnCode ReturnCode { get; set; }

        public UnexpectedAniDBResponseException()
        {
            
        }

        public UnexpectedAniDBResponseException(UDPReturnCode code, string response) : base(
            $"Unexpected AniDB Response: {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
        }
        
        public UnexpectedAniDBResponseException(string message, UDPReturnCode code, string response) : base(
            $"Unexpected AniDB Response: {message} -- {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
            Message = message;
        }
    }
}