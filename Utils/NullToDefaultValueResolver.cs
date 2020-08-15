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

            if (member.MemberType != MemberTypes.Property) return provider;
            Type propType = ((PropertyInfo)member).PropertyType;
            // If it can't be activated
            return new ExistingOrDefaultValueProvider(provider, propType);
        }

        private class ExistingOrDefaultValueProvider : IValueProvider
        {
            private IValueProvider innerProvider;
            private object defaultValue;

            public ExistingOrDefaultValueProvider(IValueProvider innerProvider, Type propType)
            {
                this.innerProvider = innerProvider;
                defaultValue = propType.GetConstructor(Type.EmptyTypes) != null || propType
                    .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                    .Any(x => x.GetParameters().All(p => p.IsOptional))
                    ? Activator.CreateInstance(propType)
                    : default;
            }

            public void SetValue(object target, object value)
            {
                innerProvider.SetValue(target, value ?? innerProvider.GetValue(target) ?? defaultValue);
            }

            public object GetValue(object target)
            {
                return innerProvider.GetValue(target) ?? defaultValue;
            }
        }
    }
}