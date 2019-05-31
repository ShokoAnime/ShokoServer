using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Shoko.Server.API.v1
{
    public class StreamWithResponse : Stream, IStreamWithResponse//, IActionResult
    {
        public Stream Stream { get; set; }
        [Obsolete("Use Controller.", true)]
        public string ContentType { get; set; }
        [Obsolete("Use Controller.Response.StatusCode", true)]
        public HttpStatusCode ResponseStatus { get; set; }
        [Obsolete("Use Controller.", true)]
        public string ResponseDescription { get; set; }
        [Obsolete("Use Controller.", true)]
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();
        public long ContentLength { get; set; }

        public bool HasContent => Stream != null;

        public StreamWithResponse(Stream stream, string contentType)
        {
            Stream = stream;
            //ContentType = contentType;
            //ResponseStatus = HttpStatusCode.OK;
        }

        public StreamWithResponse(Stream stream, string contentType, HttpStatusCode responseStatus)
        {
            Stream = stream;
            //ContentType = contentType;
            //ResponseStatus = responseStatus;
        }

        public StreamWithResponse(HttpStatusCode responseStatus, string description = null)
        {
            //ResponseStatus = responseStatus;
            //ResponseDescription = description;
        }

        public StreamWithResponse()
        {

        }

        public override void Flush()
        {
            Stream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return Stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            Stream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Stream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Stream.Write(buffer, offset, count);
        }

        /*public Task ExecuteResultAsync(ActionContext context)
        {
            var resp = context.HttpContext.Response;
            foreach (var header in Headers)
            {
                if (!resp.Headers.ContainsKey(header.Key))
                    resp.Headers.Add(header.Key, header.Value);
                else
                    resp.Headers[header.Key] = header.Value;
            }

            resp.ContentLength = ContentLength;
            resp.StatusCode = (int)ResponseStatus;


            return Task.CompletedTask;
        }*/

        public override bool CanRead => Stream.CanRead;
        public override bool CanSeek => Stream.CanSeek;
        public override bool CanWrite => Stream.CanWrite;
        public override long Length => Stream.Length;

        public override long Position
        {
            get { return Stream.Position; }
            set { Stream.Position = value; }
        }
    }

    public interface IStreamWithResponse
    {
        string ContentType { get; }
        HttpStatusCode ResponseStatus { get; }
        string ResponseDescription { get; }
        Dictionary<string, string> Headers { get; }
        long ContentLength { get; }

        bool HasContent { get; }
    }
}
