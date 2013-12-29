using System.IO;

namespace JMMServer
{
    internal class SubStream : Stream
    {
        private long _bytesLeft;
        private Stream _stream;
        private long _pos;
        private long _original;

        public override void Close()
        {
            _stream.Close();
        }

        public SubStream(Stream stream, long offset, long bytesToRead)
        {
            _stream = stream;
            _pos = 0;
            _stream.Seek(offset, SeekOrigin.Begin);
            _bytesLeft = bytesToRead;
            _original = bytesToRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
            }
        }


        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new System.NotImplementedException();
        }

        public override long Length
        {
            get { return _original; }
        }

        public override long Position
        {
            get { return _pos; }
            set { throw new System.NotImplementedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count > _bytesLeft)
            {
                count = (int) _bytesLeft;
            }
            int read = _stream.Read(buffer, offset, count);
            _pos += read;
            _bytesLeft -= read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }
    }
}