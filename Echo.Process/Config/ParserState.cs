using System;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.Config
{
    public class ParserState
    {
        public readonly HashMap<string, ClusterToken> Clusters;
        public readonly HashMap<string, ValueToken> Locals;

        public ParserState(
            HashMap<string, ClusterToken> clusters,
            HashMap<string, ValueToken> locals
            )
        {
            Clusters = clusters;
            Locals = locals;
        }

        public ParserState AddCluster(string alias, ClusterToken cluster) =>
            new ParserState(Clusters.AddOrUpdate(alias, cluster), Locals);

        public ParserState SetClusters(HashMap<string, ClusterToken> clusters) =>
            new ParserState(clusters, Locals);

        public bool LocalExists(string name) =>
            Locals.ContainsKey(name);

        public ParserState AddLocal(string name, ValueToken value) =>
            new ParserState(Clusters, Locals.Add(name,value));

        public Option<ValueToken> Local(string name) =>
            Locals.Find(name);

        public static readonly ParserState Empty = new ParserState(HashMap<string,ClusterToken>(), HashMap<string, ValueToken>());
    }
}
