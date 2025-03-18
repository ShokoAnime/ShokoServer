#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Quartz;
using Sentry;

namespace Shoko.Server.Settings.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class EnvironmentConfigAttribute : ValidationAttribute
{
    public string EnvironmentVariable { get; set; }

    public EnvironmentConfigAttribute(string environmentVariable)
    {
        EnvironmentVariable = environmentVariable;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) == null)
            return base.IsValid(value, validationContext);

        return new($"Environment variable {EnvironmentVariable} is set, changing this is not allowed.");
    }

    public bool TryGet<T>(out T? value) where T : ISpanParsable<T>
    {
        var env = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (env is null)
        {
            value = default;
            return false;
        }
        
        return T.TryParse(env, null, out value);
    }

    public bool TryGet(Type targetType, out object? result)
    {
        result = null;
        
        var input = Environment.GetEnvironmentVariable(EnvironmentVariable);
    
        if (string.IsNullOrEmpty(input))
            return false;

        var spanParseableInterface = targetType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && 
                i.GetGenericTypeDefinition() == typeof(ISpanParsable<>));
    
        if (spanParseableInterface == null)
            return false;
    
        MethodInfo? tryParseMethod = null;
    
        foreach (var method in targetType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "TryParse")
                continue;

            var parameters = method.GetParameters();
            
            if (parameters.Length == 2 &&
                // ReSharper disable once BuiltInTypeReferenceStyle
                parameters[0].ParameterType == typeof(String) &&
                parameters[1].IsOut &&
                parameters[1].ParameterType.IsByRef &&
                parameters[1].ParameterType.GetElementType() == targetType)
            {
                tryParseMethod = method;
                break;
            }
        }
        
        if (tryParseMethod == null)
            return false;
    
        var callingParameters = new object?[2];
        callingParameters[0] = input;
        callingParameters[1] = null;
    
        var success = (bool)tryParseMethod.Invoke(null, callingParameters)!;
    
        if (success)
            result = callingParameters[1];
    
        return success;
    }
}
