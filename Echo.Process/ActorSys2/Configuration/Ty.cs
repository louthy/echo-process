using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public abstract record Ty
    {
        public abstract Context<bool> Equiv(Ty rhs);
        public abstract string Show();
    }

    public record TyNil : Ty
    {
        public static readonly Ty Default = new TyNil(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyNil || rhs is TyArr);

        public override string Show() =>
            "[]";
    }

    public record TyArray(Ty Type) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyNil     => Context.Pure(true),
                TyArray r => Type.Equiv(r.Type),
                _         => Context.Pure(false)
            };

        public override string Show() =>
            $"[{Type.Show()}]";
    }

    public record TyVar(string Name) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyVar (var n) && Name == n);

        public override string Show() =>
            $"{Name}";
    }

    public record TyArr(Ty X, Ty Y) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyArr mr
                ? from l in X.Equiv(Y)
                  from r in mr.X.Equiv(mr.Y)
                  select l && r
                : Context.Pure(false);

        public override string Show() =>
            $"{X} → {Y}";
    }

    // public record TyRef(Ty Type) : Ty
    // {
    //     
    // }

    public record FieldTy(string Name, Ty Type)
    {
        public string Show() =>
            $"{Name} : {Type}";
    }

    public record TyProcess(TyRecord Value) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyProcess rp => Value.Equiv(rp.Value),
                TyRecord rr  => Value.Equiv(rr),
                _            => Context.Pure(false)
            };

        public override string Show() =>
            "process";
    }

    public record TyRouter(TyRecord Value) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyRouter rp => Value.Equiv(rp.Value),
                TyRecord rr => Value.Equiv(rr),
                _           => Context.Pure(false)
            };

        public override string Show() =>
            "router";
    }

    public record TyCluster(TyRecord Value) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyCluster rp => Value.Equiv(rp.Value),
                TyRecord rr  => Value.Equiv(rr),
                _            => Context.Pure(false)
            };

        public override string Show() =>
            "cluster";
    }

    public record TyStrategy(StrategyType Type, TyRecord Value) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs switch
            {
                TyStrategy rp => Type == rp.Type
                                    ? Value.Equiv(rp.Value)
                                    : Context.Pure(false),
                _             => Context.Pure(false)
            };

        public override string Show() =>
            "cluster";
    }

    public record TyRecord(Seq<FieldTy> Fields) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyRecord mr
                ? Fields.Count == mr.Fields.Count
                      ? from xs in Fields.OrderBy(f => f.Name).ToSeq()
                                         .Zip(mr.Fields.OrderBy(f => f.Name).ToSeq())
                                         .Sequence(p => p.Left.Name == p.Right.Name
                                                            ? p.Left.Type.Equiv(p.Right.Type)
                                                            : Context.Pure(false))
                        select xs.ForAll(Prelude.identity)
                      : Context.Pure(false)
                : Context.Pure(false);

        public override string Show() =>
            $"record ({string.Join(", ", Fields.Map(f => f.Show()))})";
    }

    public record TyVariant(Seq<FieldTy> Fields) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyRecord mr
                ? Fields.Count == mr.Fields.Count
                      ? from xs in Fields.Zip(mr.Fields)
                                         .Sequence(p => p.Left.Name == p.Right.Name
                                                            ? p.Left.Type.Equiv(p.Right.Type)
                                                            : Context.Pure(false))
                        select xs.ForAll(Prelude.identity)
                      : Context.Pure(false)
                : Context.Pure(false);        

        public override string Show() =>
            $"variant ({string.Join(", ", Fields.Map(f => f.Show()))})";
    }

    public record TyUnit : Ty
    {
        public static readonly Ty Default = new TyUnit(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyUnit);
    
        public override string Show() =>
            $"unit";
    }

    public record TyBool : Ty
    {
        public static readonly Ty Default = new TyBool(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyBool);
    
        public override string Show() =>
            $"bool";
    }

    public record TyInt : Ty
    {
        public static readonly Ty Default = new TyInt(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyInt);
    
        public override string Show() =>
            $"int";
    }

    public record TyFloat : Ty
    {
        public static readonly Ty Default = new TyFloat(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyFloat);
    
        public override string Show() =>
            $"float";
    }

    public record TyString : Ty
    {
        public static readonly Ty Default = new TyString(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyString);
    
        public override string Show() =>
            $"string";
    }

    public record TyProcessId : Ty
    {
        public static readonly Ty Default = new TyProcessId(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyProcessId);
    
        public override string Show() =>
            $"process-id";
    }

    public record TyProcessName : Ty
    {
        public static readonly Ty Default = new TyProcessName(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyProcessName);
    
        public override string Show() =>
            $"process-name";
    }

    public record TyProcessFlag : Ty
    {
        public static readonly Ty Default = new TyProcessFlag(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyProcessFlag);
    
        public override string Show() =>
            $"process-flag";
    }

    public record TyTime : Ty    
    {
        public static readonly Ty Default = new TyTime(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyTime);
    
        public override string Show() =>
            $"time";
    }

    public record TyMessageDirective : Ty
    {
        public static readonly Ty Default = new TyMessageDirective(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyMessageDirective);
    
        public override string Show() =>
            $"message-directive";
    }

    public record TyDirective : Ty
    {
        public static readonly Ty Default = new TyDirective(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyDirective);
    
        public override string Show() =>
            $"directive";
    }

    public record TyTuple(Seq<Ty> Types) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyTuple mr
                ? Types.Zip(mr.Types)
                       .Sequence(p => p.Left.Equiv(p.Right))
                       .Map(xs => xs.ForAll(Prelude.identity))
                : Context.Pure(false);
 
        public override string Show() =>
            $"tuple ({string.Join(", ", Types.Map(f => f.Show()))})";
    }
}