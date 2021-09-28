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
        public static Decl Var(Loc Location, string Name, Prototype Prototype, Term Value) => new DeclVar(Location, Name, Prototype, Value);
        public static Decl Strategy(Loc Location, string Name, StrategyType Type, TmRecord Value) => new DeclStrategy(Location, Name, Type, Value);
        public static Decl Cluster(Loc Location, string Name, string Alias, TmRecord Value) => new DeclCluster(Location, Name, Alias, Value);
        public static Decl Router(Loc Location, string Name, TmRecord Value) => new DeclRouter(Location, Name, Value);
        public static Decl Process(Loc Location, string Name, TmRecord Value) => new DeclProcess(Location, Name, Value);
        public static Decl Record(Loc Location, string Name, TmRecord Value) => new DeclRecord(Location, Name, Value);
        public static Decl Type(Loc Location, string Name, Seq<(string Name, Kind Kind)> Vars, Ty Body) => new DeclType(Location, Name, Vars, Body);

        public abstract Context<Unit> TypeCheck();

        protected static Context<Unit> assertRequiredFieldType(TyRecord rec, string fieldName, Ty expected, Loc location) =>
            rec.Fields.Find(f => f.Name == fieldName).Case switch
            {
                FieldTy fty => from eq in fty.Type.Equiv(expected)
                               from rs in eq ? Context.Unit : Context.Fail<Unit>(ProcessError.IncorrectTypeForAttribute(location, fty.Name, fty.Type, expected))
                               select rs,
                _ => Context.Fail<Unit>(ProcessError.RequiredAttributeMissing(location, fieldName))
            };

        protected static Context<Unit> assertOptionalFieldType(TyRecord rec, string fieldName, Ty expected, Loc location) =>
            rec.Fields.Find(f => f.Name == fieldName).Case switch
            {
                FieldTy fty => from eq in fty.Type.Equiv(expected)
                               from rs in eq ? Context.Unit : Context.Fail<Unit>(ProcessError.IncorrectTypeForAttribute(location, fty.Name, fty.Type, expected))
                               select rs,
                _ => Context.Pure<Unit>(unit)
            };
    }

    public record DeclType(Loc Location, string Name, Seq<(string Name, Kind Kind)> Vars, Ty Body) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from ty in AddParameters(Vars)
            from kd in ty.KindOf(Location)
            from _x in Context.log($"ty: {ty}  ---- kind: {kd}")
            from __ in Context.addTop(Location, Name, new TyLamBind(ty, kd))
            select unit;

        Context<Ty> AddParameters(Seq<(string Name, Kind Kind)> ps) =>
            ps.IsEmpty
                ? Context.Pure(Body)
                : Context.localBinding(ps.Head.Name, TyNameBind.Default,
                                       AddParameters(ps.Tail).Map(b => (Ty) new TyLam(ps.Head.Name, ps.Head.Kind, b)));
    }

    public record DeclVar(Loc Location, string Name, Prototype Prototype, Term Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from _1 in Context.log(Prototype.ReturnType.Case)
            from tm in AddParameters(Prototype.Parameters, AddReturn(Value, Prototype)).Bind(static tm => tm.Eval)
            from _2 in Context.log(tm)
            from ty in tm.TypeOf
            from _3 in Context.log(ty)
            from __ in Context.addTop(Location, Name, new TmAbbBind(tm, ty))
            select unit;

        static Term AddReturn(Term body, Prototype prototype) =>
            prototype.ReturnType.Case switch
            {
                Ty ty => AddReturn(body, ty),
                _     => body
            };

        static Term AddReturn(Term body, Ty returnType) =>
            body switch
            {
                TmTLam tlam => Term.TLam(tlam.Location, tlam.Subject, tlam.Kind, AddReturn(tlam.Expr, returnType)),
                _           => Term.Ascribe(body, returnType)
            };

        Context<Term> AddParameters(Seq<Parameter> ps, Term body) =>
            ps.IsEmpty
                ? Context.Pure(body)
                : ps.Head.Type is TyVar tvar
                    ? from x in Context.isTyNameBound(tvar.Name)
                      from r in x
                                    ? AddParameter(ps, body)
                                    : Context.localBinding(tvar.Name, TyNameBind.Default,
                                                           AddParameter(ps, body).Map(body => Term.TLam(ps.Head.Location, tvar.Name, Kind.Star, body)))
                      select r
                    : AddParameter(ps, body);

        Context<Term> AddParameter(Seq<Parameter> ps, Term body) =>
            Context.localBinding(ps.Head.Name, TmNameBind.Default,
                                 AddParameters(ps.Tail, body).Map(
                                     body =>
                                         Term.Lam(ps.Head.Location, ps.Head.Name, ps.Head.Type, body)));
    }

    public record DeclStrategy(Loc Location, string Name, StrategyType StrategyType, TmRecord Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from tm in Value.Eval
            from ty in tm.TypeOf
            from rc in ty is TyRecord rec ? Context.Pure(rec) : Context.Fail<TyRecord>(ProcessError.StrategyTypeInvalid(Location, ty)) 
            from __ in Context.addTop(Location, Name, new TmAbbBind(tm, new TyStrategy(StrategyType, rc)))
            select unit;
    }

    public record DeclCluster(Loc Location, string Name, string Alias, TmRecord Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from tm in Value.Eval
            from ty in tm.TypeOf
            from rc in ty is TyRecord rec ? Context.Pure(rec) : Context.Fail<TyRecord>(ProcessError.ClusterTypeInvalid(Location, ty)) 
            from _1 in assertRequiredFieldType(rc, "node-name", TyString.Default, Location) 
            from _2 in assertRequiredFieldType(rc, "role", TyString.Default, Location) 
            from _3 in assertRequiredFieldType(rc, "connection", TyString.Default, Location) 
            from _4 in assertRequiredFieldType(rc, "database", TyString.Default, Location) 
            
            from _5 in assertOptionalFieldType(rc, "env", TyString.Default, Location)
            from _6 in assertOptionalFieldType(rc, "user-env", TyString.Default, Location)
            from _7 in assertOptionalFieldType(rc, "default", TyBool.Default, Location)
            
            from __ in Context.addTop(Location, string.IsNullOrWhiteSpace(Alias) ? Name : Alias, new TmAbbBind(tm, new TyCluster(rc)))
            select unit;
    }
    
    public record DeclRouter(Loc Location, string Name, TmRecord Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from tm in Value.Eval
            from ty in tm.TypeOf
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
            from __ in Context.addTop(Location, Name, new TmAbbBind(tm, new TyRouter(rc)))
            select unit;
    }
    
    public record DeclProcess(Loc Location, string Name, TmRecord Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from tm in Value.Eval
            from ty in tm.TypeOf
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
            from __ in Context.addTop(Location, Name, new TmAbbBind(tm, new TyProcess(rc)))
            select unit;
    }
    
    public record DeclRecord(Loc Location, string Name, TmRecord Value) : Decl(Location, Name)
    {
        public override Context<Unit> TypeCheck() =>
            from tm in Value.Eval
            from ty in tm.TypeOf
            from rc in ty is TyRecord rec ? Context.Pure(rec) : Context.Fail<TyRecord>(ProcessError.RecordTypeInvalid(Location, ty)) 
            from __ in Context.addTop(Location, Name, new TmAbbBind(tm, ty))
            select unit;
    }
}