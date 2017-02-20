using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Echo
{
    public class ProcessMetaData
    {
        public string[] MsgTypeNames;
        public string StateTypeName;
        public string[] StateTypeInterfaces;

        public ProcessMetaData(string[] msgTypeNames, string stateTypeName, string[] stateTypeInterfaces)
        {
            MsgTypeNames = msgTypeNames;
            StateTypeName = stateTypeName;
            StateTypeInterfaces = stateTypeInterfaces;
        }

        public Type GetStateType() =>
            Type.GetType(StateTypeName);
    }
}
