using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echo
{
    public class ClusterNode
    {
        public readonly ProcessName NodeName;
        public readonly DateTime LastHeartbeat;
        public readonly ProcessName Role;
        public readonly Version EchoVersion;

        public ClusterNode(ProcessName nodeName, DateTime lastHeartbeat, ProcessName role, Version echoVersion)
        {
            NodeName = nodeName;
            LastHeartbeat = lastHeartbeat;
            Role = role;
            EchoVersion = echoVersion;
        }
    }
}
