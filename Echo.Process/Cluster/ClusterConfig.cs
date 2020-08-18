using System;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo
{
    public class ClusterConfig
    {
        public readonly ProcessName NodeName;
        public readonly string ConnectionString;
        public readonly string CatalogueName;
        public readonly ProcessName Role;
        
        public readonly Atom<Option<IDisposable>> Connection = Atom<Option<IDisposable>>(None);

        public ClusterConfig(
            ProcessName nodeName,
            string connectionString,
            string catalogueName,
            ProcessName role
        )
        {
            NodeName = nodeName;
            ConnectionString = connectionString;
            CatalogueName = catalogueName;
            Role = role;
        }
    }
}
