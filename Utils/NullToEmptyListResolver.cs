using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace Shoko.Commons.Utils
{
    public class NullToEmptyListResolver : DefaultContractResolver
    {
        protected override IValueProvider CreateMemberValueProvider(MemberInfo member)
        {
            IValueProvider provider = base.CreateMemberValueProvider(member);

            if (member.MemberType == MemberTypes.Property)
            {
                Type propType = ((PropertyInfo)member).PropertyType;
                if (propType.IsGenericType && 
                    propType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    return new EmptyListValueProvider(provider, propType);
                }
            }

            return provider;
        }

        class EmptyListValueProvider : IValueProvider
        {
            private IValueProvider innerProvider;
            private object defaultValue;

            public EmptyListValueProvider(IValueProvider innerProvider, Type listType)
            {
                this.innerProvider = innerProvider;
                defaultValue = Activator.CreateInstance(listType);
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