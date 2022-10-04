using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Shoko.Server.Utilities;

internal class ReflectionUtils
{
    public delegate T ObjectActivator<T>(params object[] args);

    public delegate T ObjectMethodActivator<T>(object instance, params object[] args);

    public static ObjectActivator<T> GetActivator<T>
        (ConstructorInfo ctor)
    {
        var type = ctor.DeclaringType;
        var paramsInfo = ctor.GetParameters();

        //create a single param of type object[]
        var param = Expression.Parameter(typeof(object[]), "args");
        var argsExp = new Expression[paramsInfo.Length];

        //pick each arg from the params array 
        //and create a typed expression of them
        for (var i = 0; i < paramsInfo.Length; i++)
        {
            Expression index = Expression.Constant(i);
            var paramType = paramsInfo[i].ParameterType;
            Expression paramAccessorExp = Expression.ArrayIndex(param, index);
            Expression paramCastExp = Expression.Convert(paramAccessorExp, paramType);

            argsExp[i] = paramCastExp;
        }

        //make a NewExpression that calls the
        //ctor with the args we just created
        var newExp = Expression.New(ctor, argsExp);

        //create a lambda with the New
        //Expression as body and our param object[] as arg
        var lambda = Expression.Lambda(typeof(ObjectActivator<T>), newExp, param);

        //compile it
        var compiled = (ObjectActivator<T>)lambda.Compile();
        return compiled;
    }

    public static ObjectMethodActivator<T> GetMethodActivator<T>(MethodInfo mtd)
    {
        var type = mtd.DeclaringType;
        var paramInfos = mtd.GetParameters();
        var param = Expression.Parameter(typeof(object[]), "args");
        var argsExp = new Expression[paramInfos.Length];

        for (var i = 0; i < paramInfos.Length; i++)
        {
            var parameterType = paramInfos[i].ParameterType;
            argsExp[i] = Expression.Convert(Expression.ArrayIndex(param, Expression.Constant(i)), parameterType);
        }

        Expression instanceExp = Expression.Parameter(type, "instance");
        var mtdExp = Expression.Call(instanceExp, mtd, argsExp);
        LambdaExpression lambda = Expression.Lambda<ObjectMethodActivator<T>>(mtdExp, param);
        return (ObjectMethodActivator<T>)lambda.Compile();
    }

    public static ObjectMethodActivator<T> GetMethodActivator<T>(Type type, string methodName)
    {
        return GetMethodActivator<T>(type.GetMethod(methodName));
    }
}
