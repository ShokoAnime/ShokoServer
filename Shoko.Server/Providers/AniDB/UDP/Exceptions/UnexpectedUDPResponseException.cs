using System;

namespace Shoko.Server.Providers.AniDB.UDP.Exceptions
{
    [Serializable]
    public class UnexpectedUDPResponseException : Exception
    {
        public new string Message { get; set; }
        public string Response { get; set; }
        public UDPReturnCode ReturnCode { get; set; }

        public UnexpectedUDPResponseException()
        {
            
        }

        public UnexpectedUDPResponseException(UDPReturnCode code, string response) : base(
            $"Unexpected AniDB Response: {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
        }
        
        public UnexpectedUDPResponseException(string message, UDPReturnCode code, string response) : base(
            $"Unexpected AniDB Response: {message} -- {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
            Message = message;
        }
    }
}