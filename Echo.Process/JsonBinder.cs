using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Serialization;
using static LanguageExt.Prelude;

namespace Echo
{
    /// <summary>
    /// Attempts to deal with the issue of serialising between .NET Framework and .NET Core
    /// The core libraries of each framework have different names, and the serialised types
    /// have these names embedded.  This binder attempts to use the assembly and type-name
    /// provided.  If it fails then it calls Type.GetType which has the following documented
    /// behaviour:
    ///
    ///   "If the type is in the currently executing assembly or in
    ///    mscorlib.dll/System.Private.CoreLib.dll, it is sufficient to supply the type name
    ///    qualified by its namespace"
    /// 
    ///   https://docs.microsoft.com/en-us/dotnet/api/system.type.gettype?view=net-5.0
    ///
    /// It also looks for the TypeForwardedFrom attribute which is used to indicate a type
    /// moved from one assembly to another in the past.  So this acts like a redirect.  We
    /// can send both assembly-names with the messages so we can try both when deserialising. 
    /// </summary>
    public class JsonBinder : ISerializationBinder
    {
        readonly ConcurrentDictionary<(string, string), Type> types = new ();

        /// <summary>
        /// Takes an assembly-name and type-name and tries to get the concrete Type
        /// </summary>
        /// <remarks>
        /// This can handle multiple assembly names and will try them all.  If it fails then it will do a last
        /// gasp attempt at loading via the Type.GetType function (which doesn't need an assembly name in all
        /// circumstances) 
        /// </remarks>
        public Type BindToType(string asm, string tyname)
        {
            return types.AddOrUpdate((asm, tyname), add, noupdate);
            
            static Type add((string asm, string tyname) p)
            {
                var asms = p.asm.Split('|');
                foreach (var asm in asms)
                {
                    var ty = LoadTypeFromAsm(asm, p.tyname);
                    if (ty != null) return ty;
                }
                return Type.GetType(p.Item2);
            }                
            
            static Type noupdate((string, string) _, Type x) =>
                x;
        }

        /// <summary>
        /// Takes a known type and extracts an-assembly name and type-name from it
        /// </summary>
        /// <remarks>
        /// This checks for a TypeForwardedFrom attribute, which is what library writers use to indicate that the
        /// type used to exist in another assembly with a different name.  If this is the case then we return both
        /// assembly names with a '|' separator.  This means that the deserialiser can try both assembly names.
        /// </remarks>
        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            typeName     = serializedType.FullName;
            assemblyName = serializedType.Assembly.FullName;
            if (Attribute.GetCustomAttribute(serializedType, typeof(TypeForwardedFromAttribute), false) is TypeForwardedFromAttribute fromattr)
            {
                assemblyName += $"|{fromattr.AssemblyFullName}";
            }
        }

        static Type LoadTypeFromAsm(string asm, string name)
        {
            try
            {
                return Assembly.Load(asm).GetType(name);
            }
            catch
            {
                return null;
            }
        }
    }
}