using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NLog;

namespace Shoko.Server.Utilities;

public static class JsonExtensions
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Creates a list based on a JSON Array
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="jsonArray"></param>
    /// <returns></returns>
    public static IEnumerable<T> FromJSONArray<T>(this string jsonArray)
    {
        if (string.IsNullOrEmpty(jsonArray))
        {
            return new List<T>();
        }

        try
        {
            var result = JsonConvert.DeserializeObject<IEnumerable<T>>(jsonArray);
            return result ?? new List<T>();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error in Serialization: " + ex);
            return new List<T>();
        }
    }

    /// <summary>
    /// Creates an object from JSON
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="json"></param>
    /// <returns></returns>
    public static T FromJSON<T>(this string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        try
        {
            var ser = JsonConvert.DeserializeObject<T>(json);
            return ser;

            /*using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json.ToCharArray())))
            {
                var ser = new DataContractJsonSerializer(typeof(T));
                return (T)ser.ReadObject(ms);
            }*/
        }
        catch
        {
            return default;
        }
    }
}
