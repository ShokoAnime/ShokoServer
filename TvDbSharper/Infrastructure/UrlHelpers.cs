namespace TvDbSharper.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    internal class UrlHelpers  
    {
        public string Parametrify(Enum value)
        {
            var elements = value
                .ToString()
                .Split(',')
                .Select(element => this.PascalCase(element.Trim()))
                .OrderBy(element => element);

            return string.Join(",", elements);
        }

        public string PascalCase(string name)
        {
            char[] array = name.ToCharArray();

            array[0] = char.ToLower(array[0]);

            return new string(array);
        }

        public string Querify<T>(T obj)
        {
            var parts = new List<string>();

            foreach (var propertyInfo in typeof(T).GetTypeInfo().DeclaredProperties.OrderBy(info => info.Name))
            {
                object value = propertyInfo.GetValue(obj);

                if (value != null)
                {
                    parts.Add($"{this.PascalCase(propertyInfo.Name)}={Uri.EscapeDataString(value.ToString())}");
                }
            }

            return string.Join("&", parts);
        }

        public string QuerifyEnum(Enum obj)
        {
            return this.PascalCase(obj.ToString());
        }
    }
}