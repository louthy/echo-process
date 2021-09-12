using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public abstract record Ty
    {
        public abstract Context<bool> Equiv(Ty rhs);
    }

    public record TyNil : Ty
    {
        public static readonly Ty Default = new TyNil(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyNil || rhs is TyArr);
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
    }

    public record TyVar(string Name) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyVar (var n) && Name == n);
    }

    public record TyArr(Ty X, Ty Y) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyArr mr
                ? from l in X.Equiv(Y)
                  from r in mr.X.Equiv(mr.Y)
                  select l && r
                : Context.Pure(false);
    }

    public record FieldTy(string Name, Ty Type);

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
    }

    public record TyUnit : Ty
    {
        public static readonly Ty Default = new TyUnit(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyUnit);
    }

    public record TyBool : Ty
    {
        public static readonly Ty Default = new TyBool(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyBool);
    }

    public record TyInt : Ty
    {
        public static readonly Ty Default = new TyInt(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyInt);
    }

    public record TyFloat : Ty
    {
        public static readonly Ty Default = new TyFloat(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyFloat);
    }

    public record TyString : Ty
    {
        public static readonly Ty Default = new TyString(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyString);
    }

    public record TyProcessId : Ty
    {
        public static readonly Ty Default = new TyProcessId(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyProcessId);
    }

    public record TyProcessName : Ty
    {
        public static readonly Ty Default = new TyProcessName(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyProcessName);
    }

    public record TyProcessFlag : Ty
    {
        public static readonly Ty Default = new TyProcessFlag(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyProcessFlag);
    }

    public record TyTime : Ty    
    {
        public static readonly Ty Default = new TyTime(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyTime);
    }

    public record TyMessageDirective : Ty
    {
        public static readonly Ty Default = new TyMessageDirective(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyMessageDirective);
    }

    public record TyInboxDirective : Ty
    {
        public static readonly Ty Default = new TyInboxDirective(); 
        
        public override Context<bool> Equiv(Ty rhs) =>
            Context.Pure(rhs is TyInboxDirective);
    }

    public record TyTuple(Seq<Ty> Types) : Ty
    {
        public override Context<bool> Equiv(Ty rhs) =>
            rhs is TyTuple mr
                ? Types.Zip(mr.Types)
                       .Sequence(p => p.Left.Equiv(p.Right))
                       .Map(xs => xs.ForAll(Prelude.identity))
                : Context.Pure(false);
    }
}