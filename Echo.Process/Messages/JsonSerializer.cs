using Newtonsoft.Json;

namespace Echo
{
    /// <summary>
    /// A configuration for a message serializer.
    /// </summary>
    /// <remarks>You need to set Settings before you start the echo processes system.</remarks>
    public static class JsonSerializer
    {
        /// <summary>
        /// Set echo internal setup of the serializer the client does not need to be aware of.
        /// </summary>
        static JsonSerializerSettings Setup(JsonSerializerSettings settings)
        {
            settings.TypeNameHandling = TypeNameHandling.All;
            settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;
            settings.MissingMemberHandling = MissingMemberHandling.Ignore;
            return settings;
        }

        static JsonSerializerSettings settings = Setup(new JsonSerializerSettings());
        
        public static JsonSerializerSettings Settings
        {
            get => settings;
            set => settings = Setup(value ?? new JsonSerializerSettings());
        }
    }
}