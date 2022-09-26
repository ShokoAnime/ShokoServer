using System;

namespace Shoko.Server.Providers.AniDB.UDP.Exceptions
{
    [Serializable]
    public class UnexpectedUDPResponseException : Exception
    {
        public string Response { get; set; }
        public UDPReturnCode ReturnCode { get; set; }

        public UnexpectedUDPResponseException(UDPReturnCode code, string response) : base($"Unexpected AniDB Response: {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
        }
        
        public UnexpectedUDPResponseException(string message, UDPReturnCode code, string response) : base(message)
        {
            Response = response;
            ReturnCode = code;
        }

        public UnexpectedUDPResponseException(string response) : base($"Unexpected AniDB Response: {response}")
        {
            Response = response;
            ReturnCode = UDPReturnCode.UNKNOWN_COMMAND;
        }
    }
}