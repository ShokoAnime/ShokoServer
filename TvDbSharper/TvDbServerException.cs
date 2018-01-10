namespace TvDbSharper
{
    using System;

    public class TvDbServerException : Exception
    {
        public TvDbServerException(string message, int statusCode, Exception inner)
            : base(message, inner)
        {
            this.StatusCode = statusCode;
        }

        public TvDbServerException(string message, int statusCode)
            : base(message)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; }

        public bool UnknownError { get; set; }
    }
}