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

        public WritableOptions(
            IOptionsMonitor<T> options,
            IConfigurationRoot configuration,
            string section)
        {
            _options = options;
            _configuration = configuration;
            _section = section;
        }

        public T Value => _options.CurrentValue;
        public T Get(string name) => _options.Get(name);

        public void Update(Action<T> applyChanges)
        {
            var physicalPath = Path.Combine(ServerSettings.ApplicationPath, ServerSettings.SettingsFilename);

            var jObject = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(physicalPath));
            var sectionObject = jObject.TryGetValue(_section, out JToken section) ?
                JsonConvert.DeserializeObject<T>(section.ToString()) : (Value ?? new T());

            applyChanges(sectionObject);

            if (string.IsNullOrEmpty(_section))
                jObject = JObject.Parse(JsonConvert.SerializeObject(sectionObject));
            else
                jObject[_section] = JObject.Parse(JsonConvert.SerializeObject(sectionObject));

            File.WriteAllText(physicalPath, JsonConvert.SerializeObject(jObject, Formatting.Indented));
            _configuration.Reload();
        }
    }
}