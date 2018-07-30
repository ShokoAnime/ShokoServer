using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Nancy.Rest.Annotations.Atributes;
using Nancy.Rest.Annotations.Enums;
using Newtonsoft.Json;
using Shoko.Server.API.MVCRouter;
using Shoko.Server.API.v1;

namespace Shoko.Server.API
{
    internal static class ApiV1Router
    {
        private static Regex rparam = new Regex("^\\{(.*?)\\}$", RegexOptions.Compiled);

        private static Dictionary<Type, RouteCache> _cache = new Dictionary<Type, RouteCache>();

        public static IRouteBuilder RouteFor(this IRouteBuilder builder, IHttpContextAccessor obj)
        {
            string ModulePath = "";

            Type cls = obj.GetType();
            var paths = cls.GetCustomAttributesFromInterfaces<RestBasePath>().ToList();

            if (paths.Count > 0)
                ModulePath = paths[0].BasePath;


            foreach (MethodInfo m in cls.GetMethods())
            {
                Rest r = m.GetCustomAttributesFromInterfaces<Rest>().FirstOrDefault();
                if (r == null)
                    continue;

                Type[] types = m.GetParameters().Select(a => a.ParameterType).ToArray();
                MethodInfo method = cls.GetInterfaces().FirstOrDefault(a => a.GetMethod(m.Name, types) != null)?.GetMethod(m.Name, types);
                if (method == null)
                    method = m;
                (string err, List<ParamInfo> @params) result = CheckMethodAssign(method, r);

                RouteCacheItem c = new RouteCacheItem
                {
                    Verb = r.Verb,
                    Route = r.Route,
                    IsAsync = method.IsAsyncMethod(),
                    MethodInfo = method,
                    ContentType = r.ResponseContentType,
                    Parameters = result.@params
                };
                string path = ModulePath + (ModulePath[ModulePath.Length - 1] == '/' || r.Route[0] == '/' ? "" : "/") + r.Route;

                builder.MapVerb(r.Verb.ToString(), path, async (request, response, routeData) => 
                {
                    obj.HttpContext = request.HttpContext;

                    List<object> paramList = new List<object>();

                    foreach (var param in result.@params)
                        paramList.Add(routeData.Values[param.Name]);

                    var invocation = method.Invoke(obj, paramList.ToArray());

                    if (invocation is ActionResult<object> action)
                    {
                        await action.Result.ExecuteResultAsync(new ActionContext(request.HttpContext, routeData, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()));
                        invocation = action.Value;
                    }

                    if (invocation is ActionResult act)
                    {
                        await act.ExecuteResultAsync(new ActionContext(request.HttpContext, routeData, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()));
                        return;
                    }

                    if (invocation is StreamWithResponse respStr)
                    {
                        response.ContentType = respStr.ContentType;
                        response.ContentLength = respStr.ContentLength;
                        response.StatusCode = (int)respStr.ResponseStatus;

                        foreach (var pair in respStr.Headers)
                        {
                            response.Headers[pair.Key] = pair.Value;
                        }

                        if (respStr.HasContent)
                        {
                            response.Body = respStr.Stream;
                            return;
                        }
                    }

                    switch (request.Headers["accept"].FirstOrDefault()?.ToLower())
                    {
                        case "application/xml":
                            await response.WriteAsync(invocation.XmlSerializeObject());
                            break;
                        default:
                            await response.WriteAsync(JsonConvert.SerializeObject(invocation));
                            break;
                    }
                });
            }

            return builder;
        }

        public static string XmlSerializeObject<T>(this T toSerialize)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(toSerialize.GetType());

            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, toSerialize);
                return textWriter.ToString();
            }
        }

        internal static void AddToCache<T>(T obj)
        {
            lock (_cache)
            {
                RouteCache tc = new RouteCache {Items = new List<RouteCacheItem>()};

                Type cls = obj.GetType();
                var paths = cls.GetCustomAttributesFromInterfaces<RestBasePath>().ToList();

                if (paths.Count > 0)
                    tc.ModulePath = paths[0].BasePath;
                _cache.Add(cls, tc);

                foreach (MethodInfo m in cls.GetMethods())
                {
                    Rest r = m.GetCustomAttributesFromInterfaces<Rest>().FirstOrDefault();
                    if (r == null)
                        continue;

                    Type[] types = m.GetParameters().Select(a => a.ParameterType).ToArray();
                    MethodInfo method = cls.GetInterfaces().FirstOrDefault(a => a.GetMethod(m.Name, types) != null)?.GetMethod(m.Name, types);
                    if (method == null)
                        method = m;
                    (string err, List<ParamInfo> @params) result = CheckMethodAssign(method, r);

                    RouteCacheItem c = new RouteCacheItem
                    {
                        Verb = r.Verb,
                        Route = r.Route,
                        IsAsync = method.IsAsyncMethod(),
                        MethodInfo = method,
                        ContentType = r.ResponseContentType,
                        Parameters = result.Item2
                    };
                    tc.Items.Add(c);
                    
                }

                _cache.Add(cls, tc);
            }
        }

        private static Regex rpath = new Regex("\\{(.*?)\\}", RegexOptions.Compiled);

        private static (string err, List<ParamInfo> @params) CheckMethodAssign(MethodInfo minfo, Nancy.Rest.Annotations.Atributes.Rest attribute)
        {
            List<ParamInfo> parms = new List<ParamInfo>();
            MatchCollection collection = rpath.Matches(attribute.Route);
            foreach (Match m in collection)
            {
                if (m.Success)
                {
                    string value = m.Groups[1].Value;
                    bool optional = false;
                    string constraint = null;
                    int idx = value.LastIndexOf("?", StringComparison.InvariantCulture);
                    if (idx > 0)
                    {
                        value = value.Substring(0, idx);
                        optional = true;
                    }
                    idx = value.LastIndexOf(':');
                    if (idx >= 0)
                    {
                        constraint = value.Substring(idx + 1);
                        value = value.Substring(0, idx);
                        idx = constraint.LastIndexOf("(", StringComparison.InvariantCulture);
                        if (idx > 0)
                            constraint = constraint.Substring(0, idx);
                    }
                    ParamInfo info = new ParamInfo();
                    info.Name = value;
                    info.Optional = optional;
                    if (constraint != null)
                    {
                        ParameterType ptype = ParameterType.InstanceTypes.FirstOrDefault(a => a.Name == constraint);
                        if (ptype == null)
                            return ("Method with Name: '" + minfo.Name + "' and Route: '" + attribute.Route + "' has an unknown constraint '" + constraint + "'", null);
                        info.Constraint = ptype;
                    }
                    parms.Add(info);
                }
            }
            List<ParameterInfo> infos = minfo.GetParameters().ToList();
            foreach (ParamInfo p in parms)
            {
                ParameterInfo pinf = infos.FirstOrDefault(a => a.Name == p.Name);
                if (pinf == null)
                    return ("Method with Name: '" + minfo.Name + "' and Route: '" + attribute.Route + "' has an unknown variable in the route path '" + p.Name + "'", null);
                if (p.Optional && !pinf.ParameterType.IsNullable())
                    return ("Method with Name: '" + minfo.Name + "' and Route: '" + attribute.Route + "' with variable '" + p.Name + "' is marked in the route path as nullable, but the method variable is not", null);
                if (p.Constraint != null && !p.Constraint.Types.Contains(pinf.ParameterType))
                    return ("Method with Name: '" + minfo.Name + "' and Route: '" + attribute.Route + "' with variable '" + p.Name + "' is constrained to type " + p.Constraint.BaseType + " but the method variable is not of the same type", null);
                if (!pinf.ParameterType.IsRouteable())
                    return ("Method with Name: '" + minfo.Name + "' and Route: '" + attribute.Route + "' the variable '" + p.Name + "' is not a value type and is the route path", null);
                infos.Remove(pinf);
            }
            if (infos.Count > 0 && attribute.Verb == Verbs.Get)
            {
                return ("Method with Name: '" + minfo.Name + "' and Route: '" + attribute.Route + "' has post variables in a GET operation", null);
            }
            if (infos.Count > 1)
            {
                return ("Method with Name: '" + minfo.Name + "' and Route: '" + attribute.Route + "' has more than one Body object", null);
            }
            return (null, parms);
        }
    }
}