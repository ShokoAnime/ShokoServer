using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Shoko.Server.Settings.Source
{
    class JsonProvider : FileConfigurationProvider
    {
        public JsonProvider(FileConfigurationSource source) : base(source)
        {
        }

        public override void Load(Stream stream)
        {
            Data = JsonParser.Parse(stream);
        }
    }
}
