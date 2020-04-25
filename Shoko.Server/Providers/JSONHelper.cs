using System;
using Newtonsoft.Json;

namespace Shoko.Server.Providers
{
    public static class JSONHelper
    {
        public static string Serialize<T>(T obj)
        {
            var retVal = JsonConvert.SerializeObject(obj);
            return retVal;
        }

        public static T Deserialize<T>(string json)
        {
            var obj = JsonConvert.DeserializeObject<T>(json,
                new JsonSerializerSettings
                {
                    EqualityComparer = StringComparer.InvariantCultureIgnoreCase,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                });
            return obj;
        }
    }
}