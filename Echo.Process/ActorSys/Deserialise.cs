﻿using Newtonsoft.Json;
using System;
using System.Reflection;
using static LanguageExt.Prelude;
using System.Linq;
using LanguageExt;

namespace Echo
{
    /// <summary>
    /// Helper function for invoking the generic JsonConvert.DeserializeObject function
    /// instead of the variant that takes a Type argument.  This forces the type to be
    /// cast away from JObject and gives the caller the best chance of getting a useful
    /// value.
    /// </summary>
    internal static class Deserialise
    {
        static HashMap<string, MethodInfo> funcs = HashMap<string, MethodInfo>();

        static MethodInfo Ignore() =>
            null;

        static MethodInfo DeserialiseFunc(Type type)
        {
            var name = type.FullName;
            var result = funcs.Find(name);
            if (result.IsSome) return result.IfNoneUnsafe(Ignore);

            var func = typeof(JsonConvert).GetTypeInfo()
                                   .GetDeclaredMethods("DeserializeObject")
                                   .Filter(m => m.IsGenericMethod)
                                   .Filter(m => m.GetParameters().Length == 2)
                                   .Filter(m => m.GetParameters().ElementAt(1).ParameterType.Equals(typeof(JsonSerializerSettings)))
                                   .Head()
                                   .MakeGenericMethod(type);

            // No locks because we don't really care if it's done
            // more than once, but we do care about locking unnecessarily.
            funcs = funcs.AddOrUpdate(name, func);
            return func;
        }

        public static T Object<T>(string value) =>
            JsonConvert.DeserializeObject<T>(value);

        public static object Object(string value, Type type) =>
            DeserialiseFunc(type).Invoke(null, new object[] { value, ActorSystemConfig.Default.JsonSerializerSettings });
    }
}