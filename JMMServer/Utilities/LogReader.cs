using System.IO;
using System.Text;

namespace JMMServer.Utilities
{
    class LogReader : TextReader
    {
        private TextReader _baseReader;
        private int _position;

        public LogReader(FileStream stream, int offset)
        {
            if (offset > 0)
            {
                stream.Seek(offset, SeekOrigin.Begin);
            }
            // Forcing ascii here allows not to care about multi-byte encodings 
            // as we read full lines anyways but offset is in bytes
            TextReader baseReader = new StreamReader(stream,Encoding.ASCII);
            _baseReader = baseReader;
        }

        public override int Read()
        {
            _position++;
            return _baseReader.Read();
        }

        public override int Peek()
        {
            return _baseReader.Peek();
        }

        public int Position
        {
            get { return _position; }
        }
    }
}
