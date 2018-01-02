using System;
using Newtonsoft.Json;
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
            if (objectType == typeof(Commons.Plex.Libraries.Directory))
                instance = new SVR_Directory(_helper);
            else if (objectType == typeof(Commons.Plex.TVShow.Episode))
                instance = new SVR_Episode(_helper);
            else if (objectType == typeof(Commons.Plex.Collection.PlexLibrary))
                instance = new SVR_PlexLibrary(_helper);

            //var instance = objectType.GetConstructor(new[] { typeof(PlexHelper) })?.Invoke(new object[] { _helper });
            serializer.Populate(reader, instance);
            return instance;
        }

        public override bool CanConvert(Type objectType) =>
            objectType == typeof(Commons.Plex.Libraries.Directory) || objectType == typeof(Commons.Plex.TVShow.Episode) ||
            objectType == typeof(Commons.Plex.Collection.PlexLibrary);
    }
}