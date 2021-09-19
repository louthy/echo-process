using LanguageExt;
using static LanguageExt.Prelude;

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

        public abstract Context<Unit> TypeCheck();

        protected static Context<Unit> assertRequiredFieldType(TyRecord rec, string fieldName, Ty expected, Loc location) =>
            rec.Fields.Find(f => f.Name == fieldName).Case switch
            {
                FieldTy fty => from eq in fty.Type.Equiv(expected)
                               from rs in eq ? Context.Pure(unit) : Context.Fail<Unit>(ProcessError.IncorrectTypeForAttribute(location, fty.Name, fty.Type, expected))
                               select rs,
                _ => Context.Fail<Unit>(ProcessError.RequiredAttributeMissing(location, fieldName))
            };

        protected static Context<Unit> assertOptionalFieldType(TyRecord rec, string fieldName, Ty expected, Loc location) =>
            rec.Fields.Find(f => f.Name == fieldName).Case switch
            {
                FieldTy fty => from eq in fty.Type.Equiv(expected)
                               from rs in eq ? Context.Pure(unit) : Context.Fail<Unit>(ProcessError.IncorrectTypeForAttribute(location, fty.Name, fty.Type, expected))
                               select rs,
                _ => Context.Pure<Unit>(unit)
            };
    }

    public record DeclGlobalVar(Loc Location, string Name, Term Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from ty in Value.TypeOf
            from __ in Context.addTop(Location, Name, new VarBind(ty))
            select unit;
    }

    public record DeclStrategy(Loc Location, string Name, StrategyType Type, TmRecord Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from ty in Value.TypeOf
            from rc in ty is TyRecord rec ? Context.Pure(rec) : Context.Fail<TyRecord>(ProcessError.StrategyTypeInvalid(Location, ty)) 
            from __ in Context.addTop(Location, Name, new VarBind(new TyStrategy(Type, rc)))
            select unit;
    }

    public record DeclCluster(Loc Location, string Name, string Alias, TmRecord Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from ty in Value.TypeOf
            from rc in ty is TyRecord rec ? Context.Pure(rec) : Context.Fail<TyRecord>(ProcessError.ClusterTypeInvalid(Location, ty)) 
            from _1 in assertRequiredFieldType(rc, "node-name", TyString.Default, Location) 
            from _2 in assertRequiredFieldType(rc, "role", TyString.Default, Location) 
            from _3 in assertRequiredFieldType(rc, "connection", TyString.Default, Location) 
            from _4 in assertRequiredFieldType(rc, "database", TyString.Default, Location) 
            
            from _5 in assertOptionalFieldType(rc, "env", TyString.Default, Location)
            from _6 in assertOptionalFieldType(rc, "user-env", TyString.Default, Location)
            from _7 in assertOptionalFieldType(rc, "default", TyBool.Default, Location)
            
            from __ in Context.addTop(Location, string.IsNullOrWhiteSpace(Alias) ? Name : Alias, new VarBind(new TyCluster(rc)))
            select unit;
    }
    
    public record DeclRouter(Loc Location, string Name, TmRecord Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from ty in Value.TypeOf
            from rc in ty is TyRecord rec ? Context.Pure(rec) : Context.Fail<TyRecord>(ProcessError.RouterTypeInvalid(Location, ty)) 
            from _1 in assertRequiredFieldType(rc, "pid", TyProcessId.Default, Location) 
            from _2 in assertOptionalFieldType(rc, "flags", TyProcessFlag.Default, Location) 
            from _3 in assertOptionalFieldType(rc, "mailbox-size", TyInt.Default, Location) 
            from _4 in assertOptionalFieldType(rc, "dispatch", TyString.Default, Location) 
            from _5 in assertOptionalFieldType(rc, "route", TyString.Default, Location) 
            from _6 in assertOptionalFieldType(rc, "register-as", TyString.Default, Location) 
          //from _7 in assertRequiredFieldType(rc, "strategy", TyRecord.Default, Location)        // TODO: Is a strategy record 
          //from _8 in assertRequiredFieldType(rc, "workers", TyRecord.Default, Location)         // TODO: Is a process record
            from _9 in assertOptionalFieldType(rc, "worker-count", TyInt.Default, Location) 
            from _a in assertOptionalFieldType(rc, "worker-name", TyString.Default, Location) 
            from __ in Context.addTop(Location, Name, new VarBind(new TyRouter(rc)))
            select unit;
    }
    
    public record DeclProcess(Loc Location, string Name, TmRecord Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from ty in Value.TypeOf
            from rc in ty is TyRecord rec ? Context.Pure(rec) : Context.Fail<TyRecord>(ProcessError.ProcessTypeInvalid(Location, ty)) 
            from _1 in assertRequiredFieldType(rc, "pid", TyProcessId.Default, Location) 
            from _2 in assertOptionalFieldType(rc, "flags", TyProcessFlag.Default, Location) 
            from _3 in assertOptionalFieldType(rc, "mailbox-size", TyInt.Default, Location) 
            from _4 in assertOptionalFieldType(rc, "dispatch", TyString.Default, Location) 
            from _5 in assertOptionalFieldType(rc, "route", TyString.Default, Location) 
            from _6 in assertOptionalFieldType(rc, "register-as", TyString.Default, Location) 
          //from _7 in assertRequiredFieldType(rc, "strategy", TyRecord.Default, Location)        // TODO: Is a strategy record 
          //from _8 in assertRequiredFieldType(rc, "workers", TyRecord.Default, Location)         // TODO: Is a process record
            from _9 in assertOptionalFieldType(rc, "worker-count", TyInt.Default, Location) 
            from _a in assertOptionalFieldType(rc, "worker-name", TyString.Default, Location) 
            from __ in Context.addTop(Location, Name, new VarBind(new TyProcess(rc)))
            select unit;
    }
    
    public record DeclRecord(Loc Location, string Name, TmRecord Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from ty in Value.TypeOf
            from rc in ty is TyRecord rec ? Context.Pure(rec) : Context.Fail<TyRecord>(ProcessError.RecordTypeInvalid(Location, ty)) 
            from __ in Context.addTop(Location, Name, new VarBind(ty))
            select unit;
    }
}