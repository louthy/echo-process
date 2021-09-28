using System;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public abstract record Ty
    {
        public abstract Context<bool> Equiv(Ty rhs);
        public abstract string Show();

        public virtual Context<Ty> Compute() =>
            Context.NoRuleAppliesTy;
        
        public virtual Context<Ty> Simplify() =>
            from t in Compute()
            from s in t.Simplify() | @catch(ProcessError.NoRuleApplies, t)
            select t;

        public virtual Ty Subst(string name, Ty ty) =>
            this;

        public virtual Context<Kind> KindOf(Loc location) =>
            Context.StarKind;

        public Seq<TyVar> GetVars() =>
            GetVarsSeq().Strict().Distinct();
        
        internal virtual Seq<TyVar> GetVarsSeq() =>
            Empty;

        public Ty Ref(Ty Type) =>
            new TyRef(Type);

        public static Ty All(string Subject, Kind Kind, Ty Type) =>
            new TyAll(Subject, Kind, Type);

        public static Ty Some(string Subject, Kind Kind, Ty Type) =>
            new TySome(Subject, Kind, Type);

        public static Ty Lam(string Subject, Kind Kind, Ty Type) =>
            new TyLam(Subject, Kind, Type);

        public static Ty App(Ty X, Ty Y) =>
            new TyApp(X, Y);

        public static Ty Var(string Name) =>
            new TyVar(Name);

        public static Ty Id(string Name) =>
            new TyId(Name);

        public static Ty Arr(Ty X, Ty Y) =>
            new TyArr(X, Y);

        public static Ty Array(Ty Type) =>
            new TyArray(Type);

        public static Ty Record(Seq<FieldTy> Fields) =>
            new TyRecord(Fields);

        public static Ty Tuple(Seq<Ty> Types) =>
            new TyTuple(Types);

        public static Ty Process(TyRecord Value) =>
            new TyProcess(Value);

        public static Ty Cluster(TyRecord Value) =>
            new TyCluster(Value);

        public static Ty Router(TyRecord Value) =>
            new TyRouter(Value);

        public static Ty Strategy(StrategyType Type, TyRecord Value) =>
            new TyStrategy(Type, Value);

        public static Ty Variant(Seq<FieldTy> Fields) =>
            new TyVariant(Fields);

        public static readonly Ty Nil = new TyNil();
        public static readonly Ty Unit = new TyUnit();
        public static readonly Ty Bool = new TyBool();
        public static readonly Ty Int = new TyInt();
        public static readonly Ty Float = new TyFloat();
        public static readonly Ty String = new TyString();
        public static readonly Ty MessageDirective = new TyMessageDirective();
        public static readonly Ty Directive = new TyDirective();
        public static readonly Ty Time = new TyTime();
        public static readonly Ty ProcessName = new TyProcessName();
        public static readonly Ty ProcessId = new TyProcessId();
        public static readonly Ty ProcessFlag = new TyProcessFlag();
    }

    /// <summary>
    /// Ref type 
    /// </summary>
    public record TyRef(Ty Type) : Ty
    {
        public override Ty Subst(string name, Ty type) =>
            new TyRef(Type.Subst(name, type));

        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyRef tref => Type.Equiv(tref.Type),
                _          => Context.False
            };

        public override string Show() =>
            $"ref {Type.Show()}";
       
        internal override Seq<TyVar> GetVarsSeq() =>
            Type.GetVarsSeq();

        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Universal qualified type.  Examples:
    ///
    ///     ∀ (a :: *). a → a
    ///     ∀ (f :: * => *). (a :: *). (r :: *). f a → r
    /// 
    /// </summary>
    public record TyAll(string Subject, Kind Kind, Ty Type) : Ty
    {
        public override Ty Subst(string name, Ty type) =>
            name == Subject 
                ? this  // Don't wipe out local alls
                : new TyAll(Subject, Kind, Type.Subst(name, type));

        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyAll rall && Kind == rall.Kind
                ? Context.localBinding(Subject, TyNameBind.Default,
                                       Type.Equiv(rall.Type))
                : Context.False;

        public override string Show() =>
            $"forall {Subject} :: {Kind.Show()}. {Type.Show()}";

        public override Context<Kind> KindOf(Loc location) =>
            Context.localBinding(Subject, new TyVarBind(Kind),
                                 Type.KindOf(location).Bind(
                                     k => k == Kind.Star
                                              ? Context.StarKind
                                              : Context.Fail<Kind>(ProcessError.StarKindExpected(location))));
       
        internal override Seq<TyVar> GetVarsSeq() =>
            new TyVar(Subject).Cons(Type.GetVarsSeq());

        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Existential  type.  Examples:
    ///
    ///     ∃ (a :: *). a → a
    ///     ∃ (f :: * => *). (a :: *). (r :: *). f a → r
    /// 
    /// </summary>
    public record TySome(string Subject, Kind Kind, Ty Type) : Ty
    {
        public override Ty Subst(string name, Ty type) =>
            name == Subject 
                ? this // Don't wipe out local somes
                : new TySome(Subject, Kind, Type.Subst(name, type));
                
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TySome rsome && Kind == rsome.Kind
                ? Context.localBinding(Subject, TyNameBind.Default, Type.Equiv(rsome.Type))
                : Context.False;

        public override string Show() =>
            $"exists {Subject} :: {Kind.Show()} . {Type.Show()}";

        public override Context<Kind> KindOf(Loc location) =>
            Context.localBinding(Subject, new TyVarBind(Kind),
                                 Type.KindOf(location).Bind(
                                     k => k == Kind.Star
                                              ? Context.StarKind
                                              : Context.Fail<Kind>(ProcessError.StarKindExpected(location))));
       
        internal override Seq<TyVar> GetVarsSeq() =>
            new TyVar(Subject).Cons(Type.GetVarsSeq());
 
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Type lambda abstraction, used to represent generics as functions that take types to produce other types
    ///
    ///     (a :: *) => Bool
    /// 
    /// </summary>
    public record TyLam(string Subject, Kind Kind, Ty Type) : Ty
    {
        public override Ty Subst(string name, Ty type) =>
            Subject == name
                ? new TyLam(name, Kind, Type.Subst(name, type))
                : new TyLam(Subject, Kind, Type.Subst(name, type));

        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyLam rlam when Kind == rlam.Kind =>
                    Context.localBinding(Subject, TyNameBind.Default, 
                                         Type.Equiv(rlam.Type)),

                TyVar tvar => Context.getTyLam(tvar.Name).Bind(this.Equiv) | @catch(ProcessError.NoRuleApplies, false),
                _          => Context.False
            };
        
        public override string Show() =>
            $"{Subject} :: {Kind.Show()} => {Type.Show()}";

        public override Context<Kind> KindOf(Loc location) =>
            Context.localBinding(Subject, new TyVarBind(Kind), 
                                 Type.KindOf(location).Map(k2 => Kind.Arr(Kind, k2)));
       
        internal override Seq<TyVar> GetVarsSeq() =>
            new TyVar(Subject).Cons(Type.GetVarsSeq());
   
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Type application, used to apply type arguments to type lambda abstractions to create concrete types 
    /// </summary>
    public record TyApp(Ty X, Ty Y) : Ty
    {
        public override Context<Ty> Compute() =>
            X switch
            {
                TyLam (var x, _, var body) => Context.Pure(body.Subst(x, Y)),
                _                          => Context.NoRuleAppliesTy
            };
 
        public override Context<Ty> Simplify() =>
            from x in X.Simplify()
            from tyt in Context.Pure<Ty>(new TyApp(x, Y))
            from res in tyt.Simplify()
            select res; 
        
        public override Ty Subst(string name, Ty type) =>
            new TyApp(X.Subst(name, type), Y.Subst(name, type));
        
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyApp rapp
                ? from x in X.Equiv(rapp.X)
                  from y in Y.Equiv(rapp.Y)
                  select x && y
                : Context.False;

        public override string Show() =>
            $"{X.Show()} {Y.Show()}";

        public override Context<Kind> KindOf(Loc location) =>
            from kx in X.KindOf(location)
            from ky in Y.KindOf(location)
            from kn in kx switch
                       {
                           KnArr (var k1, var k2) =>
                                ky == k1
                                    ? Context.Pure(k2)
                                    : Context.Fail<Kind>(ProcessError.ParameterKindMismatch(location, k1, k2)),
                           _ => Context.Fail<Kind>(ProcessError.ArrowKindExpected(location)), 
                       }
            select kn;
       
        internal override Seq<TyVar> GetVarsSeq() =>
            X.GetVarsSeq() + Y.GetVarsSeq();
   
        public override string ToString() =>
            Show();
    }
    
    /// <summary>
    /// Type variable
    /// </summary>
    public record TyVar(string Name) : Ty
    {
        public override Context<Ty> Compute() =>
            Context.getTyLam(Name);
 
        public override Context<bool> Equiv(Ty rhs) =>
            from isTyAbb in Context.isTyLam(Name)
            from result in isTyAbb
                               ? Context.getTyLam(Name).Bind(t => t.Equiv(rhs))
                               : Context.Pure(rhs is TyVar (var n) && Name == n)
            select result;

        public override string Show() =>
            Name;

        public override Ty Subst(string name, Ty type) =>
            Name == name ? type : this;
        
        public override Context<Kind> KindOf(Loc location) =>
            Context.getKind(location, Name);
       
        internal override Seq<TyVar> GetVarsSeq() =>
            Seq1(this);
    
        public override string ToString() =>
            Show();
    }
        
    /// <summary>
    /// Type identifier
    /// </summary>
    public record TyId(string Name) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyId (var n) && Name == n);

        public override string Show() =>
            Name;
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Arrow type (function)
    /// </summary>
    public record TyArr(Ty X, Ty Y) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyArr mr
                ? from l in X.Equiv(Y)
                  from r in mr.X.Equiv(mr.Y)
                  select l && r
                : Context.False;

        public override string Show() =>
            $"{X.Show()} -> {Y.Show()}";
        
        public override Ty Subst(string name, Ty type) =>
            new TyArr(X.Subst(name, type), Y.Subst(name, type));

        public override Context<Kind> KindOf(Loc location) =>
            from x in X.KindOf(location)
            from y in Y.KindOf(location)
            from r in x == Kind.Star && y == Kind.Star
                          ? Context.StarKind
                          : Context.Fail<Kind>(ProcessError.StarKindExpected(location))
            select r;

        internal override Seq<TyVar> GetVarsSeq() =>
            X.GetVarsSeq() + Y.GetVarsSeq();
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Represents an empty array 
    /// </summary>
    public record TyNil : Ty
    {
        public static readonly Ty Default = new TyNil(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyNil => Context.True,
                TyArr => Context.True,
                _     => Context.False
            };

        public override string Show() =>
            "[]";
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Represents an array 
    /// </summary>
    public record TyArray(Ty Type) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyNil     => Context.True,
                TyArray r => Type.Equiv(r.Type),
                _         => Context.False
            };

        public override string Show() =>
            $"[{Type.Show()}]";
        
        public override Ty Subst(string name, Ty type) =>
            new TyArray(Type.Subst(name, type));
        
        public override Context<Kind> KindOf(Loc location) =>
            Type.KindOf(location);

        internal override Seq<TyVar> GetVarsSeq() =>
            Type.GetVarsSeq();
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Named type (for fields in records)
    /// </summary>
    public record FieldTy(string Name, Ty Type)
    {
        public string Show() =>
            $"{Name} : {Type.Show()}";
        
        public Context<Kind> KindOf(Loc location) =>
            Type.KindOf(location);

        internal Seq<TyVar> GetVarsSeq() =>
            Type.GetVarsSeq();
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Record type
    /// </summary>
    public record TyRecord(Seq<FieldTy> Fields) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyRecord mr
                ? Fields.Count == mr.Fields.Count
                      ? from xs in Fields.OrderBy(f => f.Name).ToSeq()
                                         .Zip(mr.Fields.OrderBy(f => f.Name).ToSeq())
                                         .Sequence(p => p.Left.Name == p.Right.Name
                                                            ? p.Left.Type.Equiv(p.Right.Type)
                                                            : Context.False)
                        select xs.ForAll(Prelude.identity)
                      : Context.False
                : Context.False;

        public override string Show() =>
            $"record ({string.Join(", ", Fields.Map(f => f.Show()))})";

        public override Ty Subst(string name, Ty type) =>
            new TyRecord(Fields.Map(f => new FieldTy(f.Name, f.Type.Subst(name, type))));

        public override Context<Kind> KindOf(Loc location) =>
            from star in Fields.Sequence(f => f.KindOf(location).Map(k => k == Kind.Star)).Map(fs => fs.ForAll(identity))
            from kind in star ? Context.StarKind : Context.Fail<Kind>(ProcessError.StarKindExpected(location))
            select kind;

        internal override Seq<TyVar> GetVarsSeq() =>
            Fields.Bind(static f => f.GetVarsSeq());
    
        public override string ToString() =>
            Show();
    }
    
    /// <summary>
    /// Tuple type
    /// </summary>
    public record TyTuple(Seq<Ty> Types) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyTuple mr
                ? Types.Zip(mr.Types)
                       .Sequence(p => p.Left.Equiv(p.Right))
                       .Map(xs => xs.ForAll(Prelude.identity))
                : Context.False;
 
        public override string Show() =>
            $"tuple ({string.Join(", ", Types.Map(f => f.Show()))})";

        public override Ty Subst(string name, Ty type) =>
            new TyTuple(Types.Map(t => t.Subst(name, type)));

        public override Context<Kind> KindOf(Loc location) =>
            from star in Types.Sequence(f => f.KindOf(location).Map(k => k == Kind.Star)).Map(fs => fs.ForAll(identity))
            from kind in star ? Context.StarKind : Context.Fail<Kind>(ProcessError.StarKindExpected(location))
            select kind;

        internal override Seq<TyVar> GetVarsSeq() =>
            Types.Bind(t => t.GetVarsSeq());
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Process type
    /// </summary>
    public record TyProcess(TyRecord Value) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyProcess rp => Value.Equiv(rp.Value),
                TyRecord rr  => Value.Equiv(rr),
                _            => Context.False
            };

        public override string Show() =>
            "process";
        
        public override Ty Subst(string name, Ty type) =>
            new TyProcess((TyRecord)Value.Subst(name, type));
            
        public override Context<Kind> KindOf(Loc location) =>
            Value.KindOf(location);

        internal override Seq<TyVar> GetVarsSeq() =>
            Value.GetVarsSeq();
   
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Router type
    /// </summary>
    public record TyRouter(TyRecord Value) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyRouter rp => Value.Equiv(rp.Value),
                TyRecord rr => Value.Equiv(rr),
                _           => Context.False
            };

        public override string Show() =>
            "router";
        
        public override Ty Subst(string name, Ty type) =>
            new TyRouter((TyRecord)Value.Subst(name, type));
            
        public override Context<Kind> KindOf(Loc location) =>
            Value.KindOf(location);

        internal override Seq<TyVar> GetVarsSeq() =>
            Value.GetVarsSeq();
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Cluster type
    /// </summary>
    public record TyCluster(TyRecord Value) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyCluster rp => Value.Equiv(rp.Value),
                TyRecord rr  => Value.Equiv(rr),
                _            => Context.False
            };

        public override string Show() =>
            "cluster";
        
        public override Ty Subst(string name, Ty type) =>
            new TyCluster((TyRecord)Value.Subst(name, type));
            
        public override Context<Kind> KindOf(Loc location) =>
            Value.KindOf(location);

        internal override Seq<TyVar> GetVarsSeq() =>
            Value.GetVarsSeq();
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Strategy type
    /// </summary>
    public record TyStrategy(StrategyType Type, TyRecord Value) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyStrategy rp => Type == rp.Type
                                    ? Value.Equiv(rp.Value)
                                    : Context.False,
                _             => Context.False
            };

        public override string Show() =>
            "strategy";
        
        public override Ty Subst(string name, Ty type) =>
            new TyStrategy(Type, (TyRecord)Value.Subst(name, type));
            
        public override Context<Kind> KindOf(Loc location) =>
            Value.KindOf(location);

        internal override Seq<TyVar> GetVarsSeq() =>
            Value.GetVarsSeq();
    
        public override string ToString() =>
            Show();
    }

    public record TyVariant(Seq<FieldTy> Fields) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyVariant mr
                ? Fields.Count == mr.Fields.Count
                      ? from xs in Fields.Zip(mr.Fields)
                                         .Sequence(p => p.Left.Name == p.Right.Name
                                                            ? p.Left.Type.Equiv(p.Right.Type)
                                                            : Context.False)
                        select xs.ForAll(Prelude.identity)
                      : Context.False
                : Context.False;        

        public override string Show() =>
            $"variant ({string.Join(", ", Fields.Map(f => f.Show()))})";

        public override Ty Subst(string name, Ty type) =>
            new TyVariant(Fields.Map(f => new FieldTy(f.Name, f.Type.Subst(name, type))));

        public override Context<Kind> KindOf(Loc location) =>
            from star in Fields.Sequence(f => f.KindOf(location).Map(k => k == Kind.Star)).Map(fs => fs.ForAll(identity))
            from kind in star ? Context.StarKind : Context.Fail<Kind>(ProcessError.StarKindExpected(location))
            select kind;

        internal override Seq<TyVar> GetVarsSeq() =>
            Fields.Bind(f => f.GetVarsSeq());
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Unit type
    /// </summary>
    public record TyUnit : Ty
    {
        public static readonly Ty Default = new TyUnit();

        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyUnit => Context.True,
                _      => Context.False
            };
    
        public override string Show() =>
            $"unit";
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Boolean type
    /// </summary>
    public record TyBool : Ty
    {
        public static readonly Ty Default = new TyBool(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyBool => Context.True,
                _      => Context.False
            };
    
        public override string Show() =>
            $"bool";
    
        public override string ToString() =>
            Show();
    }
    
    /// <summary>
    /// Integer type
    /// </summary>
    public record TyInt : Ty
    {
        public static readonly Ty Default = new TyInt(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyInt => Context.True,
                _      => Context.False
            };
    
        public override string Show() =>
            $"int";
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Floating point type
    /// </summary>
    public record TyFloat : Ty
    {
        public static readonly Ty Default = new TyFloat(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyFloat => Context.True,
                _     => Context.False
            };
    
        public override string Show() =>
            $"float";
   
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// String type
    /// </summary>
    public record TyString : Ty
    {
        public static readonly Ty Default = new TyString(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyString => Context.True,
                _        => Context.False
            };
    
        public override string Show() =>
            $"string";
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Process ID type
    /// </summary>
    public record TyProcessId : Ty
    {
        public static readonly Ty Default = new TyProcessId();

        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyProcessId => Context.True,
                _           => Context.False
            };
    
        public override string Show() =>
            $"process-id";
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Process Name type
    /// </summary>
    public record TyProcessName : Ty
    {
        public static readonly Ty Default = new TyProcessName();

        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyProcessName => Context.True,
                _             => Context.False
            };
    
        public override string Show() =>
            $"process-name";
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Process Flag type
    /// </summary>
    public record TyProcessFlag : Ty
    {
        public static readonly Ty Default = new TyProcessFlag(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyProcessFlag => Context.True,
                _             => Context.False
            };
    
        public override string Show() =>
            $"process-flag";
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Time type
    /// </summary>
    public record TyTime : Ty    
    {
        public static readonly Ty Default = new TyTime();

        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyTime => Context.True,
                _      => Context.False
            };
    
        public override string Show() =>
            $"time";
    
        public override string ToString() =>
            Show();
    }

    /// <summary>
    /// Message directive type
    /// </summary>
    public record TyMessageDirective : Ty
    {
        public static readonly Ty Default = new TyMessageDirective();

        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyMessageDirective => Context.True,
                _                  => Context.False
            };
    
        public override string Show() =>
            $"message-directive";
    
        public override string ToString() =>
            Show();
    }
    
    /// <summary>
    /// Directive type
    /// </summary>
    public record TyDirective : Ty
    {
        public static readonly Ty Default = new TyDirective();

        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyDirective => Context.True,
                _           => Context.False
            };
        
        public override string Show() =>
            $"directive";
    
        public override string ToString() =>
            Show();
    }
}