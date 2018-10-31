using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using static LanguageExt.Prelude;
using static Echo.Process;

namespace Echo.Session
{
    public static class SessionDataTypeResolve
    {
        static Map<string, Option<Type>> sessionDataTypeValid = Map<string, Option<Type>>();
        static Set<string> sessionDataTypeValidityLogged = Set<string>();
        static object sync = new object();

        internal enum TypeCastFailedStatus
        {
            TypeInvalid,
            DeserialiseFailed
        }

        /// <summary>
        /// tries to get a type from a typename. None if cannot be resolved.
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        static Option<Type> GetTypeFromName(string typeName) =>
            Try(() => Type.GetType(typeName))
                .ToOption();

        static Either<TypeCastFailedStatus, Type> GetTypeValidity(string typeName)
        {
            lock (sync)
            {
                var type = Option<Type>.None;
                (sessionDataTypeValid, type) = sessionDataTypeValid.FindOrAdd(typeName, () => GetTypeFromName(typeName));
                return type.IsSome
                    ? Right<TypeCastFailedStatus, Type>((Type)type)
                    : Left<TypeCastFailedStatus, Type>(TypeCastFailedStatus.TypeInvalid);
            }
        }

        static Unit LogTypeInvalid(string typeName)
        {
            if (!sessionDataTypeValidityLogged.Contains(typeName))
            {
                sessionDataTypeValidityLogged = sessionDataTypeValidityLogged.AddOrUpdate(typeName);
                logErr($"Session-value type invalid for this AppDomain: {typeName}");
            }
            return unit;
        }

        static Unit LogDeserialiseFailed(string value)
        {
            logErr($"Session-value is null or failed to deserialise: {value}");
            return unit;
        }

        internal static Func<TypeCastFailedStatus, Unit> DeserialiseFailed(string value, string type) => status =>
          status == TypeCastFailedStatus.TypeInvalid ? LogTypeInvalid(type)
        : status == TypeCastFailedStatus.DeserialiseFailed ? LogDeserialiseFailed(value)
        : unit;


        static Either<TypeCastFailedStatus, object> Deserialise(string value, Type type) =>
            (from t in Try(() => Echo.Deserialise.Object(value, type))
             where notnull(t)
             select t)
            .ToEither(_ => TypeCastFailedStatus.DeserialiseFailed);

        internal static Either<TypeCastFailedStatus, object> TryDeserialise(string value, string typeName) =>
            (from t in GetTypeValidity(typeName)
             from o in Deserialise(value, t)
             select o);

    }
}
