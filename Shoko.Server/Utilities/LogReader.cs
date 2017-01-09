using System.IO;
using System.Text;

namespace Shoko.Server.Utilities
{
    class LogReader : TextReader
    {
        private TextReader _baseReader;
        private int _position;
        private Encoding _encoding;

        public LogReader(FileStream stream, int offset)
        {
            if (offset > 0)
            {
                stream.Seek(offset, SeekOrigin.Begin);
                _position = offset;
            }
            _encoding = Encoding.UTF8;
            TextReader baseReader = new StreamReader(stream, _encoding);
            _baseReader = baseReader;
        }

        public override int Read() {
            int val = _baseReader.Read();
            try
            {
                char c = System.Convert.ToChar(val);
                _position += _encoding.GetByteCount(new char[] { c });
            } catch { }
            return val;
        }

        public int Position
        {
            get { return _position; }
        }
    }
}
