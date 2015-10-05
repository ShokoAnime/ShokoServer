using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMModels
{
    public class Language
    {
        public string Id { get; set; }
    }

    public class AudioLanguage : Language
    {
        public string Codec { get; set; }
        public string Bitrate { get; set; }
    }
}
