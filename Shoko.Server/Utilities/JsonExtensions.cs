﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
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
            /*var settings = new JsonSerializerSettings
            {
                Error = (sender, args) =>
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Diagnostics.Debugger.Break();
                    }
                }
            };*/

            //var result = JsonConvert.DeserializeObject<IEnumerable<T>>(jsonArray, settings);
            var result = JsonConvert.DeserializeObject<IEnumerable<T>>(jsonArray);
            return result ?? new List<T>();

            /*using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonArray)))
            {
                var ser = new DataContractJsonSerializer(typeof(IEnumerable<T>));
                var result = (IEnumerable<T>)ser.ReadObject(ms);

                if (result == null)
                {
                    return new List<T>();
                }
                else
                {
                    return result;
                }
            }*/
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
