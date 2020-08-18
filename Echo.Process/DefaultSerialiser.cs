using System;
using LanguageExt;
using static LanguageExt.Prelude;
using Newtonsoft.Json;

namespace Echo
{
    // TODO: Build a serialiser/deserialiser than is compact, efficient, and can handle structural types 
    
    public struct DefaultSerialiseIO : SerialiseIO
    {
        public static readonly SerialiseIO Default = new DefaultSerialiseIO(); 
        
        public string Serialise<A>(A value) =>
            JsonConvert.SerializeObject(value, ActorSystemConfig.Default.JsonSerializerSettings);

        public Option<A> DeserialiseExact<A>(string value)
        {
            try
            {
                Deserialise.Object<A>(value);
            }
            catch (Exception e)
            {
                return None;
            }
        }

        public Option<A> DeserialiseStructural<A>(string value) =>
            DeserialiseExact<A>(value);

        public Option<object> DeserialiseExact(string value, Type type)
        {
            try
            {
                Deserialise.Object(value, type);
            }
            catch (Exception e)
            {
                return None;
            }
        }

        public Option<object> DeserialiseStructural(string value, Type type) =>
            DeserialiseExact(value, type);
    }
}