using System;
using Newtonsoft.Json;
using Shoko.Models.Plex.Collection;
using Shoko.Server.Plex.Collection;
using Shoko.Server.Plex.Libraries;
using Shoko.Server.Plex.TVShow;

namespace Shoko.Server.Plex
{
    class PlexConverter : JsonConverter
    {
        private readonly PlexHelper _helper;

        public PlexConverter(PlexHelper helper)
        {
            _helper = helper;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            object instance = null;
            if (objectType == typeof(Shoko.Models.Plex.Libraries.Directory))
                instance = new SVR_Directory(_helper);
            else if (objectType == typeof(Shoko.Models.Plex.TVShow.Episode))
                instance = new SVR_Episode(_helper);
            else if (objectType == typeof(PlexLibrary))
                instance = new SVR_PlexLibrary(_helper);

            //var instance = objectType.GetConstructor(new[] { typeof(PlexHelper) })?.Invoke(new object[] { _helper });
            serializer.Populate(reader, instance);
            return instance;
        }

        public override bool CanConvert(Type objectType) =>
            objectType == typeof(Shoko.Models.Plex.Libraries.Directory) || objectType == typeof(Shoko.Models.Plex.TVShow.Episode) ||
            objectType == typeof(PlexLibrary);
    }
}