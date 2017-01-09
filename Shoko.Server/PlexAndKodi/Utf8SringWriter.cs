using System.IO;
using System.Text;

namespace Shoko.Server.PlexAndKodi
{
    public class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}