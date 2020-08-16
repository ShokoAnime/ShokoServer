using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace Shoko.Commons.Utils
{
    public class NullToDefaultValueResolver : DefaultContractResolver
    {
        protected override IValueProvider CreateMemberValueProvider(MemberInfo member)
        {
            IValueProvider provider = base.CreateMemberValueProvider(member);

            switch (member.MemberType)
            {
                case MemberTypes.Field:
                {
                    Type propType = ((FieldInfo)member).FieldType;
                    return new ExistingOrDefaultValueProvider(provider, propType, member.Name);
                }
                case MemberTypes.Property:
                {
                    Type propType = ((PropertyInfo)member).PropertyType;
                    return new ExistingOrDefaultValueProvider(provider, propType, member.Name);
                }
                default:
                    return base.CreateMemberValueProvider(member);
            }
        }

        private class ExistingOrDefaultValueProvider : IValueProvider
        {
            private readonly IValueProvider _innerProvider;
            private readonly object _defaultValue;
            private readonly string Name;

            public ExistingOrDefaultValueProvider(IValueProvider innerProvider, Type propType, string name)
            {
                _innerProvider = innerProvider;
                _defaultValue = propType.GetConstructor(Type.EmptyTypes) != null || propType
                    .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                    .Any(x => x.GetParameters().All(p => p.IsOptional))
                    ? Activator.CreateInstance(propType)
                    : default;
                Name = name;
            }

            public void SetValue(object target, object value)
            {
                // Could be handled as "(value ?? _innerProvider.GetValue(target)) ?? _defaultValue"
                // I think this shows more explicit definition of the order of fallback
                object result = value;
                result ??= _innerProvider.GetValue(target);
                result ??= _defaultValue;
                _innerProvider.SetValue(target, result);
            }

            public object GetValue(object target)
            {
                return _innerProvider.GetValue(target) ?? _defaultValue;
            }
        }
    }
}