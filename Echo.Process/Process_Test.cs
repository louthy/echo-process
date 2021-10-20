using LanguageExt;
using Newtonsoft.Json;
using System;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class Process
    {
        /// <summary>
        /// Support for unit testing message serialisation
        /// </summary>
        public static Validation<string, Msg> WillMyMessageSerialiseAndIsEqual<Msg>(Msg msg) =>
            from m in WillMyMessageSerialise(msg)
            from e in msg.Equals(m)
                ? Success<string, Msg>(m)
                : Fail<string, Msg>($"Deserialised {typeof(Msg)} message is not equal to inputted message")
            select e;

        /// <summary>
        /// Support for unit testing message serialisation
        /// </summary>
        public static Validation<string, Msg> WillMyMessageSerialise<Msg>(Msg msg)
        {
            try
            {
                var json = JsonConvert.SerializeObject(msg, ActorSystemConfig.Default.JsonSerializerSettings);
                try
                {
                    var msg2 = (Msg)Deserialise.Object(json, typeof(Msg));
                    if (msg2 == null)
                    {
                        return Fail<string, Msg>($"{typeof(Msg)} failed to deserialise");
                    }
                    else
                    {
                        return Success<string, Msg>(msg2);
                    }
                }
                catch (Exception e2)
                {
                    return Fail<string, Msg>($"{typeof(Msg)} failed to deserialise: {e2.Message}");
                }
            }
            catch (Exception e1)
            {
                return Fail<string, Msg>($"{typeof(Msg)} failed to serialise: {e1.Message}");
            }
        }
    }
}
