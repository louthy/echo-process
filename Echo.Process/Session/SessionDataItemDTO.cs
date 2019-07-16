using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Echo.Session
{
    /// <summary>
    /// DTO object to store type information with session data object in redis
    /// </summary>
    class SessionDataItemDTO
    {
        public readonly string SerialisedData;
        public readonly string Type;

        public SessionDataItemDTO(string serialisedData, string type)
        {
            SerialisedData = serialisedData;
            Type = type;
        }

        public static SessionDataItemDTO Create(object data) =>
            new SessionDataItemDTO(JsonConvert.SerializeObject(data), data.GetType().AssemblyQualifiedName);
    }
}
