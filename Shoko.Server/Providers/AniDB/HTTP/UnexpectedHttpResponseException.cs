using System;
using System.Net;

namespace Shoko.Server.Providers.AniDB.Http
{
    [Serializable]
    public class UnexpectedHttpResponseException : Exception
    {
        public new string Message { get; set; }
        public string Response { get; set; }
        public HttpStatusCode ReturnCode { get; set; }

        public UnexpectedHttpResponseException()
        {
            
        }

        public UnexpectedHttpResponseException(HttpStatusCode code, string response) : base(
            $"Unexpected AniDB Response: {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
        }
        
        public UnexpectedHttpResponseException(string message, HttpStatusCode code, string response) : base(
            $"Unexpected AniDB Response: {message} -- {code} | {response}")
        {
            Response = response;
            ReturnCode = code;
            Message = message;
        }
    }
}