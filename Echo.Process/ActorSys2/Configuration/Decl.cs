namespace Echo.ActorSys2.Configuration
{
    public enum StrategyType
    {
        OneForOne,
        AllForOne
    }

    public abstract record Decl(Loc Location, string Name)
    {
        public static Decl GlobalVar(Loc Location, string Name, Term Value) => new DeclGlobalVar(Location, Name, Value);
        public static Decl Strategy(Loc Location, string Name, StrategyType Type, TmRecord Value) => new DeclStrategy(Location, Name, Type, Value);
        public static Decl Cluster(Loc Location, string Name, string Alias, TmRecord Value) => new DeclCluster(Location, Name, Alias, Value);
        public static Decl Router(Loc Location, string Name, TmRecord Value) => new DeclRouter(Location, Name, Value);
        public static Decl Process(Loc Location, string Name, TmRecord Value) => new DeclProcess(Location, Name, Value);
        public static Decl Record(Loc Location, string Name, TmRecord Value) => new DeclRecord(Location, Name, Value);
    }

    public record DeclGlobalVar(Loc Location, string Name, Term Value) : Decl(Location, Name);
    public record DeclStrategy(Loc Location, string Name, StrategyType Type, TmRecord Value) : Decl(Location, Name);
    public record DeclCluster(Loc Location, string Name, string Alias, TmRecord Value) : Decl(Location, Name);
    public record DeclRouter(Loc Location, string Name, TmRecord Value) : Decl(Location, Name);
    public record DeclProcess(Loc Location, string Name, TmRecord Value) : Decl(Location, Name);
    public record DeclRecord(Loc Location, string Name, TmRecord Value) : Decl(Location, Name);
}