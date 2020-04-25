using System;
using System.Buffers;
using System.Collections;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Shoko.Server
{
    public class EmitEmptyEnumerableInsteadOfNullAttribute : ActionFilterAttribute
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new EmitEmptyEnumerableInsteadOfNullResolver
            {
                NamingStrategy = new DefaultNamingStrategy()
            },
            ObjectCreationHandling = ObjectCreationHandling.Replace
        };

        public override void OnActionExecuted(ActionExecutedContext ctx)
        {
            if (!(ctx.Result is ObjectResult objectResult)) return;
            // It would be nice if we could cache this somehow, but IDK
            objectResult.Formatters.Add(new JsonOutputFormatter(SerializerSettings,
                ctx.HttpContext.RequestServices.GetRequiredService<ArrayPool<char>>()));
        }
    }
    
    public class EmitEmptyEnumerableInsteadOfNullResolver : DefaultContractResolver
    {
        protected override IValueProvider CreateMemberValueProvider(MemberInfo member)
        {
            IValueProvider provider = base.CreateMemberValueProvider(member);

            if (member.MemberType != MemberTypes.Property) return provider;

            Type propType = ((PropertyInfo)member).PropertyType;
            // strings are enumerable, but don't count them 
            if (propType == typeof(string)) return provider;

            if (typeof(IEnumerable).IsAssignableFrom(propType))
            {
                return new EmptyListValueProvider(provider, propType);
            }

            return provider;
        }

        class EmptyListValueProvider : IValueProvider
        {
            private readonly IValueProvider _innerProvider;
            private readonly object _defaultValue;

            public EmptyListValueProvider(IValueProvider innerProvider, Type listType)
            {
                _innerProvider = innerProvider;
                
                _defaultValue = GetDefault(listType);
            }

            private object GetDefault(Type t)
            {
                // Get parameterless constructor
                ConstructorInfo constructorInfo = t.GetConstructor(Type.EmptyTypes);
                if (constructorInfo != null) return Activator.CreateInstance(t);
                // if there isn't one, It might be an Array
                if (t.GetTypeInfo().IsArray)
                {
                    var insideType = t.GetTypeInfo().GetElementType();
                    if (insideType == null) return GetNullOrDefault(t);
                    return Array.CreateInstance(insideType, 0);
                }
                // most of the rest have a constructor that takes a single IEnumerable, ex ReadOnly...
                Type enumerableInnerType = null;
                constructorInfo = t.GetConstructors().FirstOrDefault(a =>
                {
                    var para = a.GetParameters();
                    if (para.Length != 1) return false;
                    ParameterInfo info = para[0];
                    if (!info.ParameterType.IsAssignableFrom(typeof(IEnumerable))) return false;

                    enumerableInnerType = info.ParameterType;
                    return true;
                });

                // The rest are probably ones that require extra stuff to make, ex ILookup, this will prevent an error.
                if (constructorInfo == null || enumerableInnerType == null) return GetNullOrDefault(t);
                return constructorInfo.Invoke(new object[] {Array.CreateInstance(enumerableInnerType, 0)});
            }

            private object GetNullOrDefault(Type t)
            {
                return t.GetTypeInfo().IsValueType ? Activator.CreateInstance(t) : null;
            }

            public void SetValue(object target, object value)
            {
                _innerProvider.SetValue(target, value ?? _defaultValue);
            }

            public object GetValue(object target)
            {
                return _innerProvider.GetValue(target) ?? _defaultValue;
            }
        }
    }
}