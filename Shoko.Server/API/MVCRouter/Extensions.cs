using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Shoko.Server.API.MVCRouter
{
    internal static class Extensions
    {
        internal static List<T> GetCustomAttributesFromInterfaces<T>(this MethodInfo minfo) where T : Attribute
        {
            List<T> rests = new List<T>();
            List<Type> types = new List<Type> { minfo.DeclaringType };
            types.AddRange(minfo.DeclaringType?.GetInterfaces().ToList() ?? new List<Type>());
            foreach (Type t in types)
            {
                MethodInfo m = t.GetMethod(minfo.Name, minfo.GetParameters().Select(a => a.ParameterType).ToArray());
                if (m != null)
                    rests.AddRange(m.GetCustomAttributes(typeof(T)).Cast<T>().ToList());
            }
            return rests;

        }
        internal static List<T> GetCustomAttributesFromInterfaces<T>(this Type minfo) where T : Attribute
        {
            List<T> rests = new List<T>();
            List<Type> types = new List<Type> { minfo };
            types.AddRange(minfo.GetInterfaces());
            foreach (Type t in types)
            {
                rests.AddRange(t.GetCustomAttributes(typeof(T)).Cast<T>().ToList());
            }
            return rests;
        }

        internal static bool IsAsyncMethod(this MethodInfo minfo)
        {
            return (minfo.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null);
        }

        internal static bool IsNullable<T>(this T obj)
        {
            if (EqualityComparer<T>.Default.Equals(obj, default)) return true;
            Type type = typeof(T);
            if (!type.IsValueType) return true;
            if (Nullable.GetUnderlyingType(type) != null) return true;
            return false;
        }

        internal static bool IsNullable(this Type type)
        {
            if (!type.IsValueType) return true;
            if (Nullable.GetUnderlyingType(type) != null) return true;
            return false;

        }

        internal static bool IsRouteable(this Type type)
        {
            if (type.IsValueType)
                return true;
            if (type == typeof(string) || type == typeof(Guid) || type == typeof(Guid?))
                return true;
            if (Nullable.GetUnderlyingType(type)?.IsValueType ?? false)
                return true;
            return false;
        }
        public static Type GetUnderlyingType(this MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Event:
                    return ((EventInfo)member).EventHandlerType;
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                case MemberTypes.TypeInfo:
                    return ((TypeInfo)member).UnderlyingSystemType;
                default:
                    throw new ArgumentException
                    (
                     "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"
                    );
            }
        }
    }
}
