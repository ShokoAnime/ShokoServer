using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shoko.Plugin.Abstractions.Configuration
{
    public class WritableOptions<T> : IWritableOptions<T> where T : class, new()
    {
        private readonly IOptionsMonitor<T> _options;
        private readonly IConfigurationRoot _configuration;
        private readonly string _section;
        private readonly ShokoApplicationDetails _details;

        public WritableOptions(
            IOptionsMonitor<T> options,
            IConfigurationRoot configuration,
            ShokoApplicationDetails details,
            string section)
        {
            _options = options;
            _configuration = configuration;
            _details = details;
            _section = section;
        }

        public T Value => _options.CurrentValue;
        public T Get(string name) => _options.Get(name);

        public void Update(Action<T> applyChanges)
        {
            var physicalPath = Path.Combine(_details.ApplicationPath, _details.ConfigFileName);

            var jObject = JObject.Parse(File.ReadAllText(physicalPath));
            var sectionObj = Value ?? new();

            applyChanges(sectionObj);

            if (string.IsNullOrEmpty(_section))
                jObject = JObject.FromObject(sectionObj);
            else
                jObject[_section] = JObject.FromObject(sectionObj);


            File.WriteAllText(physicalPath, jObject.ToString(Formatting.Indented));
            _configuration.Reload();
        }
    }
}