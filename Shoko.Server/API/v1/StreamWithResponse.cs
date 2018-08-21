using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Shoko.Server.API.v1
{
    public class StreamWithResponse : Stream, IStreamWithResponse
    {
        public Stream Stream { get; set; }
        public string ContentType { get; set; }
        public HttpStatusCode ResponseStatus { get; set; }
        public string ResponseDescription { get; set; }
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();
        public long ContentLength { get; set; }

        public bool HasContent => Stream != null;

        public StreamWithResponse(Stream stream, string contentType)
        {
            Stream = stream;
            ContentType = contentType;
            ResponseStatus = HttpStatusCode.OK;
        }

        public StreamWithResponse(Stream stream, string contentType, HttpStatusCode responseStatus)
        {
            Stream = stream;
            ContentType = contentType;
            ResponseStatus = responseStatus;
        }

        public StreamWithResponse(HttpStatusCode responseStatus, string description = null)
        {
            ResponseStatus = responseStatus;
            ResponseDescription = description;
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
