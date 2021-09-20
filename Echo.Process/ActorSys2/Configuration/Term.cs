using System;
using System.Linq;
using System.Reactive.Subjects;
using LanguageExt;
using LanguageExt.ClassInstances.Const;
using LanguageExt.Common;
using LanguageExt.UnitsOfMeasure;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2.Configuration
{
    public abstract record Term(Loc Location)
    {
        public virtual Term Subst(string name, Term term) =>
            Subst((loc, n1, n2) => n1 == n2 ? term : this, (n, ty) => ty, name);

        public virtual Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            this;

        public Context<Term> Eval =>
            new Context<Term>(
                ctx => {

                    var t = this;
                    while (true)
                    {
                        var fnt = t.Eval1.Run(ctx);
                        if (fnt == ProcessError.NoRuleApplies) return (t, ctx);
                        var nt = fnt.ThrowIfFail();
                        t   = nt.Value;
                        ctx = nt.Context;
                    }
                });
        
        public abstract Context<Term> Eval1 { get; }

        public virtual bool IsNumeric =>
            false;

        public virtual bool IsVal =>
            IsNumeric;

        public abstract Context<Ty> TypeOf { get; }

        public static Term Array(Loc loc, Seq<Term> values) => new TmArray(loc, values);
        public static Term Tuple(Seq<Term> values) => new TmTuple(values.Head.Location, values);
        public static Term True(Loc loc) => new TmTrue(loc);
        public static Term False(Loc loc) => new TmFalse(loc);
        public static Term If(Term pred, Term @true, Term @false) => new TmIf(pred.Location, pred, @true, @false);
        public static Term Case(Term Subject, Seq<Case> Cases) => new TmCase(Subject.Location, Subject, Cases);
        public static Term Tag(string Tag, Term Term, Ty Type) => new TmTag(Term.Location, Tag, Term, Type);
        public static Term Var(Loc Location, string Name) => new TmVar(Location, Name);
        public static Term Lam(Loc Location, string Name, Ty Type, Term Body) => new TmLam(Location, Name, Type, Body);
        public static Term App(Term X, Term Y) => new TmApp(X.Location, X, Y);
        public static Term Let(Loc Location, string Name, Term Value, Term Body) => new TmLet(Location, Name, Value, Body);
        public static Term Fix(Term Term) => new TmFix(Term.Location, Term);
        public static Term String(Loc Location, string Value) => new TmString(Location, Value);
        public static Term Int(Loc Location, long Value) => new TmInt(Location, Value);
        public static Term Float(Loc Location, double Value) => new TmFloat(Location, Value);
        public static Term ProcessId(Loc Location, ProcessId Value) => new TmProcessId(Location, Value);
        public static Term ProcessName(Loc Location, ProcessName Value) => new TmProcessName(Location, Value);
        public static Term ProcessFlag(Loc Location, ProcessFlags Value) => new TmProcessFlag(Location, Value);
        public static Term Time(Loc Location, Time Value) => new TmTime(Location, Value);
        public static Term MessageDirective(Loc Location, MessageDirective Value) => new TmMessageDirective(Location, Value);
        public static Term Directive(Loc Location, Directive Value) => new TmDirective(Location, Value);
        public static Term Unit(Loc Location) => new TmUnit(Location);
        public static Term Ascribe (Term Term, Ty Type) => new TmAscribe (Term.Location, Term, Type);
        public static Term Record (Loc Location, Seq<Field> Fields) => new TmRecord (Location, Fields);
        public static Term Proj (Term Term, string Member) => new TmProj (Term.Location, Term, Member);
        public static Term Inert (Loc Location, Ty Type) => new TmInert (Location, Type);
        public static Term Named (Loc Location, string Name, Term Expr) => new TmNamed (Location, Name, Expr);
        public static Term Fail (Loc Location, Error Message) => new TmFail (Location, Message);
        public static Term Mul(Term Left, Term Right) => new TmMul(Left, Right);
        public static Term Div(Term Left, Term Right) => new TmDiv(Left, Right);
        public static Term Mod(Term Left, Term Right) => new TmMod(Left, Right);
        public static Term Sub(Term Left, Term Right) => new TmSub(Left, Right);
        public static Term Add(Term Left, Term Right) => new TmAdd(Left, Right);
        public static Term BitwiseAnd(Term Left, Term Right) => new TmBitwiseAnd(Left, Right);
        public static Term BitwiseOr(Term Left, Term Right) => new TmBitwiseOr(Left, Right);
        public static Term BitwiseXor(Term Left, Term Right) => new TmBitwiseXor(Left, Right);
        public static Term And(Term Left, Term Right) => new TmAnd(Left, Right);
        public static Term Or(Term Left, Term Right) => new TmOr(Left, Right);
        public static Term Eq(Term Left, Term Right) => new TmEq(Left, Right);
        public static Term Neq(Term Left, Term Right) => new TmNeq(Left, Right);
        public static Term Lt(Term Left, Term Right) => new TmLt(Left, Right);
        public static Term Lte(Term Left, Term Right) => new TmLte(Left, Right);
        public static Term Gt(Term Left, Term Right) => new TmGt(Left, Right);
        public static Term Gte(Term Left, Term Right) => new TmGte(Left, Right);
        public static Term Not(Term Expr) => new TmNot(Expr);
    }

    public record TmNot(Term Expr) : Term(Expr.Location)
    {
        public override Context<Term> Eval1 =>
            Expr switch
            {
                TmTrue  => Context.Pure(False(Location)),
                TmFalse => Context.Pure(True(Location)),
                var t   => t.Eval1.Map(Not)
            };

        public override Context<Ty> TypeOf =>
            from t in Expr.TypeOf
            from b in t.Equiv(TyBool.Default)
            from ty in b 
                           ? Context.Pure(TyBool.Default) 
                           : Context.Fail<Ty>(ProcessError.InvalidTypeInferred(Location, "!", t, TyBool.Default)) 
            select ty;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            Not(Expr.Subst(onVar, onType, name));
    } 
    
    public abstract record TmNumberOp(
        Term Left, 
        Term Right, 
        string Op,
        Func<Term, Term, Term> Construct, 
        Func<double, double, double> OpFloat, 
        Func<long, long, long> OpInt) : Term(Left.Location)
    {
        public override Context<Term> Eval1 =>
            (Left, Right) switch
            {
                (TmInt t1, TmInt t2)     => Context.Pure(Int(Location, OpInt(t1.Value, t2.Value))),    
                (TmFloat t1, TmFloat t2) => Context.Pure(Float(Location, OpFloat(t1.Value, t2.Value))),    
                (TmFloat t1, TmInt t2)   => Context.Pure(Float(Location, OpFloat(t1.Value, t2.Value))),    
                (TmInt t1, TmFloat t2)   => Context.Pure(Float(Location, OpFloat(t1.Value, t2.Value))),
                (TmInt t1, var t2) => from nt2 in t2.Eval1
                                      select Construct(t1, nt2),
                (TmFloat t1, var t2) => from nt2 in t2.Eval1
                                        select Construct(t1, nt2),
                var (t1, t2) => from nt1 in t1.Eval1
                                select Construct(nt1, t2),
            };

        public override Context<Ty> TypeOf =>
            from t1 in Left.TypeOf
            from t2 in Right.TypeOf
            from i1 in t1.Equiv(TyInt.Default)
            from f1 in t1.Equiv(TyFloat.Default)
            from i2 in t2.Equiv(TyInt.Default)
            from f2 in t2.Equiv(TyFloat.Default)
            from ty in (i1, f1, i2, f2) switch
                       {
                           (_, true, _, true) => Context.Pure(TyFloat.Default),
                           (true, _, true, _) => Context.Pure(TyInt.Default),
                           (_, true, true, _) => Context.Pure(TyFloat.Default),
                           (true, _, _, true) => Context.Pure(TyFloat.Default),
                           _                  => Context.Fail<Ty>(ProcessError.InvalidTypesInferred(Location, Op, t1, t2, "int or float")) 
                       }
            select ty;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            Construct(Left.Subst(onVar, onType, name), Right.Subst(onVar, onType, name));
    }

    public record TmMul(Term Left, Term Right) : TmNumberOp(Left, Right, "*", Mul, (x, y) => x * y, (x, y) => x * y);
    public record TmDiv(Term Left, Term Right) : TmNumberOp(Left, Right, "/", Div, (x, y) => x / y, (x, y) => x / y);
    public record TmMod(Term Left, Term Right) : TmNumberOp(Left, Right, "%", Mod, (x, y) => x % y, (x, y) => x % y);
    public record TmSub(Term Left, Term Right) : TmNumberOp(Left, Right, "-", Sub, (x, y) => x - y, (x, y) => x - y);
    public record TmAdd(Term Left, Term Right) : TmNumberOp(Left, Right, "+", Add, (x, y) => x + y, (x, y) => x + y);
    
    public abstract record TmBooleanOp(
        Term Left, 
        Term Right, 
        string Op, 
        Func<Term, Term, Term> Construct,
        Func<bool, bool, bool> Map) : Term(Left.Location)
    {
        public override Context<Term> Eval1 =>
            (Left, Right) switch
            {
                (TmTrue t1, TmTrue t2)   => Context.Pure(Map(true, true) ? True(Location) : False(Location)),    
                (TmFalse t1, TmTrue t2)  => Context.Pure(Map(false, true) ? True(Location) : False(Location)),
                (TmTrue t1, TmFalse t2)  => Context.Pure(Map(true, false) ? True(Location) : False(Location)),
                (TmFalse t1, TmFalse t2) => Context.Pure(Map(false, false) ? True(Location) : False(Location)),    
                (TmTrue t1, var t2)      => from nt2 in t2.Eval1
                                            select Construct(t1, nt2),
                (TmFalse t1, var t2)     => from nt2 in t2.Eval1
                                            select Construct(t1, nt2),
                var (t1, t2)             => from nt1 in t1.Eval1
                                            select Construct(nt1, t2),
            };

        public override Context<Ty> TypeOf =>
            from t1 in Left.TypeOf
            from t2 in Right.TypeOf
            from b1 in t1.Equiv(TyBool.Default)
            from b2 in t2.Equiv(TyBool.Default)
            from ty in (b1, b2) switch
                       {
                           (true, true) => Context.Pure(TyBool.Default),
                           _            => Context.Fail<Ty>(ProcessError.InvalidTypesInferred(Location, Op, t1, t2, TyBool.Default)) 
                       }
            select ty;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            Construct(Left.Subst(onVar, onType, name), Right.Subst(onVar, onType, name));
    }    

    public record TmAnd(Term Left, Term Right) : TmBooleanOp(Left, Right, "&&", And, (x, y) => x && y);
    public record TmOr(Term Left, Term Right) : TmBooleanOp(Left, Right, "||", Or, (x, y) => x || y);

    public record TmEq(Term Left, Term Right) : Term(Left.Location)
    {
        public override Context<Term> Eval1 =>
            (Left, Right) switch
            {
                (TmInt t1, TmInt t2)                           => Context.Pure(t1.Value == t2.Value ? True(Location) : False(Location)),
                (TmFloat t1, TmFloat t2)                       => Context.Pure(t1.Value == t2.Value ? True(Location) : False(Location)),
                (TmString t1, TmString t2)                     => Context.Pure(t1.Value == t2.Value ? True(Location) : False(Location)),
                (TmDirective t1, TmDirective t2)               => Context.Pure(t1.Value == t2.Value ? True(Location) : False(Location)),
                (TmMessageDirective t1, TmMessageDirective t2) => Context.Pure(t1.Value == t2.Value ? True(Location) : False(Location)),
                (TmProcessFlag t1, TmProcessFlag t2)           => Context.Pure(t1.Value == t2.Value ? True(Location) : False(Location)),
                (TmProcessId t1, TmProcessId t2)               => Context.Pure(t1.Value == t2.Value ? True(Location) : False(Location)),
                (TmProcessName t1, TmProcessName t2)           => Context.Pure(t1.Value == t2.Value ? True(Location) : False(Location)),
                (TmArray t1, TmArray t2) => t1.Values.Count == t2.Values.Count
                                                ? Context.Pure(t1.Values
                                                                 .Zip(t2.Values)
                                                                 .Map(p => Eq(p.Left, p.Right))
                                                                 .Reduce(And))
                                                : Context.Pure(False(Location)),
                (TmTuple t1, TmTuple t2) => t1.Values.Count == t2.Values.Count
                                                ? Context.Pure(t1.Values
                                                                 .Zip(t2.Values)
                                                                 .Map(p => Eq(p.Left, p.Right))
                                                                 .Reduce(And))
                                                : Context.Pure(False(Location)),
                (TmTime t1, TmTime t2)   => Context.Pure(t1.Value == t2.Value ? True(Location) : False(Location)),
                (TmUnit t1, TmUnit t2)   => Context.Pure(True(Location)),
                (TmTrue t1, TmTrue t2)   => Context.Pure(True(Location)),
                (TmTrue t1, TmFalse t2)  => Context.Pure(False(Location)),
                (TmFalse t1, TmFalse t2) => Context.Pure(True(Location)),
                (TmFalse t1, TmTrue t2)  => Context.Pure(False(Location)),
                (TmRecord t1, TmRecord t2)     => t1.Fields.Count == t2.Fields.Count && 
                                                  t1.Fields.OrderBy(f => f.Name).ToSeq().Zip(t2.Fields.OrderBy(f => f.Name).ToSeq()).ForAll(p => p.Left.Name == p.Right.Name)
                                                      ? Context.Pure(t1.Fields.OrderBy(f => f.Name).ToSeq().Zip(t2.Fields.OrderBy(f => f.Name).ToSeq())
                                                                       .Map(p => Eq(p.Left.Value, p.Right.Value))
                                                                       .Reduce(And))
                                                      : Context.Pure(False(Location)),
                (TmTag t1, TmTag t2)           => Context.Pure(Eq(t1.Term, t2.Term)),
                (TmNamed t1, TmNamed t2)       => t1.Name == t2.Name
                                                      ? Context.Pure(Eq(t1.Expr, t2.Expr))
                                                      : Context.Pure(False(Location)),
                var (t1, t2) when t1.IsVal     => t2.Eval1.Map(nt2 => Eq(t1, nt2)),
                var (t1, t2)                   => t1.Eval1.Map(nt1 => Eq(nt1, t2)),
            };

        public override Context<Ty> TypeOf =>
            from t1 in Left.TypeOf
            from t2 in Right.TypeOf
            from eq in t1.Equiv(t2)
            from ty in eq 
                           ? Context.Pure(TyBool.Default)
                           : Context.Fail<Ty>(ProcessError.InvalidComparisonType(Location, "==", t1, t2))
            select ty;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            Eq(Left.Subst(onVar, onType, name), Right.Subst(onVar, onType, name));
    }      

    public record TmNeq(Term Left, Term Right) : Term(Left.Location)
    {
        public override Context<Term> Eval1 =>
            (Left, Right) switch
            {
                (TmInt t1, TmInt t2)                           => Context.Pure(t1.Value != t2.Value ? True(Location) : False(Location)),
                (TmFloat t1, TmFloat t2)                       => Context.Pure(t1.Value != t2.Value ? True(Location) : False(Location)),
                (TmString t1, TmString t2)                     => Context.Pure(t1.Value != t2.Value ? True(Location) : False(Location)),
                (TmDirective t1, TmDirective t2)               => Context.Pure(t1.Value != t2.Value ? True(Location) : False(Location)),
                (TmMessageDirective t1, TmMessageDirective t2) => Context.Pure(t1.Value != t2.Value ? True(Location) : False(Location)),
                (TmProcessFlag t1, TmProcessFlag t2)           => Context.Pure(t1.Value != t2.Value ? True(Location) : False(Location)),
                (TmProcessId t1, TmProcessId t2)               => Context.Pure(t1.Value != t2.Value ? True(Location) : False(Location)),
                (TmProcessName t1, TmProcessName t2)           => Context.Pure(t1.Value != t2.Value ? True(Location) : False(Location)),
                (TmArray t1, TmArray t2) => t1.Values.Count == t2.Values.Count
                                                ? Context.Pure(t1.Values
                                                                 .Zip(t2.Values)
                                                                 .Map(p => Neq(p.Left, p.Right))
                                                                 .Reduce(Or))
                                                : Context.Pure(True(Location)),
                (TmTuple t1, TmTuple t2) => t1.Values.Count == t2.Values.Count
                                                ? Context.Pure(t1.Values
                                                                 .Zip(t2.Values)
                                                                 .Map(p => Neq(p.Left, p.Right))
                                                                 .Reduce(Or))
                                                : Context.Pure(True(Location)),
                (TmTime t1, TmTime t2)         => Context.Pure(t1.Value != t2.Value ? True(Location) : False(Location)),
                (TmUnit t1, TmUnit t2)         => Context.Pure(False(Location)),
                (TmTrue t1, TmTrue t2)         => Context.Pure(False(Location)),
                (TmTrue t1, TmFalse t2)        => Context.Pure(True(Location)),
                (TmFalse t1, TmFalse t2)       => Context.Pure(False(Location)),
                (TmFalse t1, TmTrue t2)        => Context.Pure(True(Location)),
                (TmRecord t1, TmRecord t2)     => t1.Fields.Count == t2.Fields.Count && 
                                                  t1.Fields.OrderBy(f => f.Name).ToSeq().Zip(t2.Fields.OrderBy(f => f.Name).ToSeq()).ForAll(p => p.Left.Name == p.Right.Name)
                                                      ? Context.Pure(t1.Fields.OrderBy(f => f.Name).ToSeq().Zip(t2.Fields.OrderBy(f => f.Name).ToSeq())
                                                                       .Map(p => Neq(p.Left.Value, p.Right.Value))
                                                                       .Reduce(Or))
                                                      : Context.Pure(True(Location)),
                (TmTag t1, TmTag t2)           => Context.Pure(Neq(t1.Term, t2.Term)),
                (TmNamed t1, TmNamed t2)       => t1.Name == t2.Name
                                                      ? Context.Pure(Neq(t1.Expr, t2.Expr))
                                                      : Context.Pure(True(Location)),
                var (t1, t2) when t1.IsVal     => t2.Eval1.Map(nt2 => Neq(t1, nt2)),
                var (t1, t2)                   => t1.Eval1.Map(nt1 => Neq(nt1, t2)),
            };

        public override Context<Ty> TypeOf =>
            from t1 in Left.TypeOf
            from t2 in Right.TypeOf
            from eq in t1.Equiv(t2)
            from ty in eq 
                           ? Context.Pure(TyBool.Default)
                           : Context.Fail<Ty>(ProcessError.InvalidComparisonType(Location, "!=", t1, t2))
            select ty;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            Neq(Left.Subst(onVar, onType, name), Right.Subst(onVar, onType, name));
    }  
    
    public record TmLt(Term Left, Term Right) : Term(Left.Location)
    {
        public override Context<Term> Eval1 =>
            (Left, Right) switch
            {
                (TmInt t1, TmInt t2)                 => Context.Pure(t1.Value < t2.Value ? True(Location) : False(Location)),
                (TmFloat t1, TmFloat t2)             => Context.Pure(t1.Value < t2.Value ? True(Location) : False(Location)),
                (TmProcessFlag t1, TmProcessFlag t2) => Context.Pure(t1.Value < t2.Value ? True(Location) : False(Location)),
                (TmTime t1, TmTime t2)               => Context.Pure(t1.Value < t2.Value ? True(Location) : False(Location)),
                (TmTag t1, TmTag t2)                 => Context.Pure(Lt(t1.Term, t2.Term)),
                (TmNamed t1, TmNamed t2)             => t1.Name == t2.Name
                                                            ? Context.Pure(Lt(t1.Expr, t2.Expr))
                                                            : Context.Pure(False(Location)),
                var (t1, t2) when t1.IsVal           => t2.Eval1.Map(nt2 => Lt(t1, nt2)),
                var (t1, t2)                         => t1.Eval1.Map(nt1 => Lt(nt1, t2)),
            };

        public override Context<Ty> TypeOf =>
            from t1 in Left.TypeOf
            from t2 in Right.TypeOf
            from eq in t1.Equiv(t2)
            from ty in eq 
                           ? Context.Pure(TyBool.Default)
                           : Context.Fail<Ty>(ProcessError.InvalidComparisonType(Location, "<", t1, t2))
            select ty;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            Lt(Left.Subst(onVar, onType, name), Right.Subst(onVar, onType, name));
    }      
    
    public record TmLte(Term Left, Term Right) : Term(Left.Location)
    {
        public override Context<Term> Eval1 =>
            (Left, Right) switch
            {
                (TmInt t1, TmInt t2)                 => Context.Pure(t1.Value <= t2.Value ? True(Location) : False(Location)),
                (TmFloat t1, TmFloat t2)             => Context.Pure(t1.Value <= t2.Value ? True(Location) : False(Location)),
                (TmProcessFlag t1, TmProcessFlag t2) => Context.Pure(t1.Value <= t2.Value ? True(Location) : False(Location)),
                (TmTime t1, TmTime t2)               => Context.Pure(t1.Value <= t2.Value ? True(Location) : False(Location)),
                (TmTag t1, TmTag t2)                 => Context.Pure(Lte(t1.Term, t2.Term)),
                (TmNamed t1, TmNamed t2)             => t1.Name == t2.Name
                                                            ? Context.Pure(Lte(t1.Expr, t2.Expr))
                                                            : Context.Pure(False(Location)),
                var (t1, t2) when t1.IsVal           => t2.Eval1.Map(nt2 => Lte(t1, nt2)),
                var (t1, t2)                         => t1.Eval1.Map(nt1 => Lte(nt1, t2)),
            };

        public override Context<Ty> TypeOf =>
            from t1 in Left.TypeOf
            from t2 in Right.TypeOf
            from eq in t1.Equiv(t2)
            from ty in eq 
                           ? Context.Pure(TyBool.Default)
                           : Context.Fail<Ty>(ProcessError.InvalidComparisonType(Location, "<=", t1, t2))
            select ty;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            Lte(Left.Subst(onVar, onType, name), Right.Subst(onVar, onType, name));
    }
    
    public record TmGt(Term Left, Term Right) : Term(Left.Location)
    {
        public override Context<Term> Eval1 =>
            (Left, Right) switch
            {
                (TmInt t1, TmInt t2)                 => Context.Pure(t1.Value > t2.Value ? True(Location) : False(Location)),
                (TmFloat t1, TmFloat t2)             => Context.Pure(t1.Value > t2.Value ? True(Location) : False(Location)),
                (TmProcessFlag t1, TmProcessFlag t2) => Context.Pure(t1.Value > t2.Value ? True(Location) : False(Location)),
                (TmTime t1, TmTime t2)               => Context.Pure(t1.Value > t2.Value ? True(Location) : False(Location)),
                (TmTag t1, TmTag t2)                 => Context.Pure(Gt(t1.Term, t2.Term)),
                (TmNamed t1, TmNamed t2)             => t1.Name == t2.Name
                                                            ? Context.Pure(Gt(t1.Expr, t2.Expr))
                                                            : Context.Pure(False(Location)),
                var (t1, t2) when t1.IsVal           => t2.Eval1.Map(nt2 => Gt(t1, nt2)),
                var (t1, t2)                         => t1.Eval1.Map(nt1 => Gt(nt1, t2)),
            };

        public override Context<Ty> TypeOf =>
            from t1 in Left.TypeOf
            from t2 in Right.TypeOf
            from eq in t1.Equiv(t2)
            from ty in eq 
                           ? Context.Pure(TyBool.Default)
                           : Context.Fail<Ty>(ProcessError.InvalidComparisonType(Location, ">", t1, t2))
            select ty;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            Gt(Left.Subst(onVar, onType, name), Right.Subst(onVar, onType, name));
    }      
    
    public record TmGte(Term Left, Term Right) : Term(Left.Location)
    {
        public override Context<Term> Eval1 =>
            (Left, Right) switch
            {
                (TmInt t1, TmInt t2)                 => Context.Pure(t1.Value >= t2.Value ? True(Location) : False(Location)),
                (TmFloat t1, TmFloat t2)             => Context.Pure(t1.Value >= t2.Value ? True(Location) : False(Location)),
                (TmProcessFlag t1, TmProcessFlag t2) => Context.Pure(t1.Value >= t2.Value ? True(Location) : False(Location)),
                (TmTime t1, TmTime t2)               => Context.Pure(t1.Value >= t2.Value ? True(Location) : False(Location)),
                (TmTag t1, TmTag t2)                 => Context.Pure(Gte(t1.Term, t2.Term)),
                (TmNamed t1, TmNamed t2)             => t1.Name == t2.Name
                                                            ? Context.Pure(Gte(t1.Expr, t2.Expr))
                                                            : Context.Pure(False(Location)),
                var (t1, t2) when t1.IsVal           => t2.Eval1.Map(nt2 => Gte(t1, nt2)),
                var (t1, t2)                         => t1.Eval1.Map(nt1 => Gte(nt1, t2)),
            };

        public override Context<Ty> TypeOf =>
            from t1 in Left.TypeOf
            from t2 in Right.TypeOf
            from eq in t1.Equiv(t2)
            from ty in eq 
                           ? Context.Pure(TyBool.Default)
                           : Context.Fail<Ty>(ProcessError.InvalidComparisonType(Location, ">=", t1, t2))
            select ty;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            Gte(Left.Subst(onVar, onType, name), Right.Subst(onVar, onType, name));
    }      
    
    public abstract record TmBitwiseOp(
        Term Left, 
        Term Right, 
        string Op,
        Func<Term, Term, Term> Construct, 
        Func<ProcessFlags, ProcessFlags, ProcessFlags> OpFlags, 
        Func<long, long, long> OpInt) : Term(Left.Location)
    {
        public override Context<Term> Eval1 =>
            (Left, Right) switch
            {
                (TmInt t1, TmInt t2)                 => Context.Pure(Int(Location, OpInt(t1.Value, t2.Value))),
                (TmProcessFlag t1, TmProcessFlag t2) => Context.Pure(ProcessFlag(Location, OpFlags(t1.Value, t2.Value))),
                (TmProcessFlag t1, TmInt t2)         => Context.Pure(ProcessFlag(Location, OpFlags(t1.Value, (ProcessFlags)t2.Value))),
                (TmInt t1, TmProcessFlag t2)         => Context.Pure(ProcessFlag(Location, OpFlags((ProcessFlags)t1.Value, t2.Value))),
                (TmInt t1, var t2)                   => from nt2 in t2.Eval1
                                                        select Construct(t1, nt2),
                (TmProcessFlag t1, var t2)           => from nt2 in t2.Eval1
                                                        select Construct(t1, nt2),
                var (t1, t2)                         => from nt1 in t1.Eval1
                                                        select Construct(nt1, t2),
            };

        public override Context<Ty> TypeOf =>
            from t1 in Left.TypeOf
            from t2 in Right.TypeOf
            from i1 in t1.Equiv(TyInt.Default)
            from f1 in t1.Equiv(TyProcessFlag.Default)
            from i2 in t2.Equiv(TyInt.Default)
            from f2 in t2.Equiv(TyProcessFlag.Default)
            from ty in (i1, f1, i2, f2) switch
                       {
                           (_, true, _, true) => Context.Pure(TyProcessFlag.Default),
                           (true, _, true, _) => Context.Pure(TyInt.Default),
                           (_, true, true, _) => Context.Pure(TyProcessFlag.Default),
                           (true, _, _, true) => Context.Pure(TyProcessFlag.Default),
                           _                  => Context.Fail<Ty>(ProcessError.InvalidTypesInferred(Location, Op, t1, t2, "int or float")) 
                       }
            select ty;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            Construct(Left.Subst(onVar, onType, name), Right.Subst(onVar, onType, name));
    }

    public record TmBitwiseAnd(Term Left, Term Right) : TmBitwiseOp(Left, Right, "&", BitwiseAnd, (x, y) => x & y, (x, y) => x & y);
    public record TmBitwiseOr(Term Left, Term Right) : TmBitwiseOp(Left, Right, "|", BitwiseOr, (x, y) => x | y, (x, y) => x | y);
    public record TmBitwiseXor(Term Left, Term Right) : TmBitwiseOp(Left, Right, "^", BitwiseXor, (x, y) => x ^ y, (x, y) => x ^ y);
    
    public record TmFail(Loc Location, Error Message) : Term(Location)
    {
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(Message);

        public override Context<Ty> TypeOf =>
            Context.Fail<Ty>(Message);

        public override bool IsVal =>
            false;

        public override bool IsNumeric =>
            false;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            this;
    }

    public record TmNamed(Loc Location, string Name, Term Expr) : Term(Location)
    {
        public override Context<Term> Eval1 =>
            from e in Expr.Eval1
            select Named(Location, Name, e);

        public override Context<Ty> TypeOf =>
            Expr.TypeOf;

        public override bool IsVal =>
            Expr.IsVal;

        public override bool IsNumeric =>
            Expr.IsNumeric;

        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmNamed(Location, Name, Expr.Subst(onVar, onType, name));
    }

    public record TmArray(Loc Location, Seq<Term> Values) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmArray(Location, Values.Map(v => v.Subst(onVar, onType, name)));
 
        public override bool IsVal =>
            Values.ForAll(static f => f.IsVal);

        public override Context<Term> Eval1 =>
            from nm in Values.ForAll(v => v.IsVal)
                           ? Context.Fail<Unit>(ProcessError.NoRuleApplies)
                           : Context.Pure(unit)
            from xs in Values.Sequence(v => v.IsVal ? Context.Pure(v) : v.Eval1)
            select new TmArray(Location, xs) as Term;

        public override Context<Ty> TypeOf =>
            Values.IsEmpty
                ? Context.Pure(TyNil.Default)
                : from ty in Values.Tail.Fold(Values.Head.TypeOf,
                                              (s, x) => from t1 in s
                                                        from t2 in x.TypeOf
                                                        from eq in t1.Equiv(t2)
                                                        from rt in eq
                                                                       ? Context.Pure(t1)
                                                                       : Context.Fail<Ty>(ProcessError.ElementsOfArrayHaveNoCommonType(Location))
                                                        select rt)
                  select new TyArray(ty) as Ty;
    }

    public record TmTuple(Loc Location, Seq<Term> Values) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmTuple(Location, Values.Map(v => v.Subst(onVar, onType, name)));
 
        public override bool IsVal =>
            Values.ForAll(static f => f.IsVal);

        public override Context<Term> Eval1 =>
            from nm in Values.ForAll(v => v.IsVal)
                           ? Context.Fail<Unit>(ProcessError.NoRuleApplies)
                           : Context.Pure(unit)
            from xs in Values.Sequence(v => v.IsVal ? Context.Pure(v) : v.Eval1)
            select new TmTuple(Location, xs) as Term;

        public override Context<Ty> TypeOf =>
            Values.IsEmpty
                ? Context.Pure(TyNil.Default)
                : from tys in Values.Sequence(v => v.TypeOf)
                  select new TyTuple(tys) as Ty;
    }
    
    public record TmTrue(Loc Location) : Term(Location)
    {
        public override bool IsVal =>
            true;

        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyBool.Default);
    }

    public record TmFalse(Loc Location) : Term(Location)
    {
        public override bool IsVal =>
            true;

        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyBool.Default);
    }

    public record TmIf(Loc Location, Term Pred, Term TrueTerm, Term FalseTerm) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmIf(Location, Pred.Subst(onVar, onType, name), TrueTerm.Subst(onVar, onType, name), FalseTerm.Subst(onVar, onType, name));

        public override Context<Term> Eval1 =>
            Pred switch
            {
                TmTrue  => Context.Pure(TrueTerm), 
                TmFalse => Context.Pure(FalseTerm),
                _       => from p in Pred.Eval1 
                           select new TmIf(Location, p, TrueTerm, FalseTerm) as Term
            };

        public override Context<Ty> TypeOf =>
            from pty in Pred.TypeOf
            from tty in TrueTerm.TypeOf
            from fty in FalseTerm.TypeOf
            from pok in pty.Equiv(TyBool.Default)
            from bok in tty.Equiv(fty)
            from res in pok
                            ? bok
                                  ? Context.Pure(tty)
                                  : Context.Fail<Ty>(ProcessError.IfBranchesIncompatible(Location))
                            : Context.Fail<Ty>(ProcessError.GuardNotBoolean(Location))
            select res;
    }

    public record Case(string Tag, string Match, Term Body);

    public record TmCase(Loc Location, Term Subject, Seq<Case> Cases) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmCase(Location, Subject.Subst(onVar, onType, name), Cases.Map(cs => new Case(cs.Tag, cs.Match, cs.Body.Subst(onVar, onType, name))));

        public override Context<Term> Eval1 =>
            Subject switch
            {
                TmTag (_, var tag, var v11, _) when v11.IsVal =>
                    Cases.Find(c => c.Tag == tag).Case switch
                    {
                        Case c => Context.Pure(c.Body),
                        _      => Context.Fail<Term>(ProcessError.NoRuleApplies)
                    },

                _ => from t1 in Subject.Eval1
                     select new TmCase(Location, t1, Cases) as Term
            };

        public override Context<Ty> TypeOf =>
            from sbj1 in Subject.TypeOf
            from sbj2 in Context.simplifyTy(sbj1)
            from resu in sbj2 switch
                         {
                             TyVariant (var fieldtys) =>
                                 from _1 in Cases.Sequence(
                                     c => fieldtys.Find(fty => fty.Name == c.Tag).Case switch
                                          {
                                              FieldTy fty => Context.Pure<Unit>(unit),
                                              _           => Context.Fail<Unit>(ProcessError.MissingCase(Location, c.Tag))
                                          })
                                 from _2 in fieldtys.Sequence(
                                     fty => Cases.Find(c => fty.Name == c.Tag).Case switch
                                            {
                                                Case cas => Context.Pure<Unit>(unit),
                                                _        => Context.Fail<Unit>(ProcessError.UnknownCase(Location, fty.Name))
                                            })
                                 from rty in fieldtys.Tail.Fold(Context.Pure(fieldtys.Head.Type),
                                                                (s, f) => from t1 in s
                                                                          from eq in t1.Equiv(f.Type)
                                                                          from ty in eq ? Context.Pure(t1) : Context.Fail<Ty>(ProcessError.BranchesOfCaseHaveNoCommonType(Location))
                                                                          select t1)
                                 select rty,

                             _ => Context.Fail<Ty>(ProcessError.ExpectedVariantType(Location))
                         }
            select resu;
    }

    public record TmTag(Loc Location, string TagName, Term Term, Ty Type) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmTag(Location, TagName, Term.Subst(onVar, onType, name), onType(name, Type));

        public override bool IsVal =>
            Term.IsVal;

        public override Context<Term> Eval1 =>
            from t in Term.Eval1
            select new TmTag(Location, TagName, t, Type) as Term;

        public override Context<Ty> TypeOf =>
            from type in Context.simplifyTy(Type)
            from resu in type switch
                         {
                             TyVariant (var fieldTys) =>
                                 from tyTiExpected in fieldTys.Find(f => f.Name == TagName).Case switch
                                                      {
                                                          FieldTy fty => Context.Pure(fty.Type),
                                                          _           => Context.Fail<Ty>(ProcessError.UnknownCase(Location, TagName))
                                                      }
                                 from tyTi in Term.TypeOf
                                 from eq in tyTi.Equiv(tyTiExpected)
                                 from rty in eq ? Context.Pure(Type) : Context.Fail<Ty>(ProcessError.CaseTypeMismatch(Location, TagName))
                                 select rty,
                             _ => Context.Fail<Ty>(ProcessError.AnnotationNotVariantType(Location))
                         }
            select resu;
    }

    public record TmVar(Loc Location, string Name) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            onVar(Location, name, Name);

        public override Context<Term> Eval1 =>
            from b in Context.getBinding(Location, Name)
            from r in b switch
                      {
                          TmAbbBind(var t, _) => Context.Pure(t),
                          _                   => Context.Fail<Term>(ProcessError.NoRuleApplies)
                      }
            select r;

        public override Context<Ty> TypeOf =>
            Context.getType(Location, Name);
    }

    public record TmLam(Loc Location, string Name, Ty Type, Term Body) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmLam(Location, Name, onType(name, Type), Body.Subst(onVar, onType, name));

        public override bool IsVal =>
            true;

        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.local(ctx => ctx.AddLocal(Name, new VarBind(Type)), 
                          from bty in Body.TypeOf
                          select new TyArr(Type,bty) as Ty);
    }

    public record TmApp(Loc Location, Term X, Term Y) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmApp(Location, X.Subst(onVar, onType, name), Y.Subst(onVar, onType, name));

        public override Context<Term> Eval1 =>
            X switch
            {
                TmLam(_, var x, var ty, var body) when Y.IsVal => 
                    Context.Pure(body.Subst(x, Y)),
                
                var v1 when v1.IsVal =>
                    from t2 in Y.Eval1
                    select new TmApp(Location, v1, t2) as Term,

                _ =>  
                    from t1 in X.Eval1
                    select new TmApp(Location, t1, Y) as Term
            };

        public override Context<Ty> TypeOf =>
            from fun in X.TypeOf
            from arg in Y.TypeOf
            from sty in Context.simplifyTy(fun)
            from res in sty switch
                        {
                            TyArr (var param, var resTy) =>
                                from eq in arg.Equiv(param)
                                from rt in eq ? Context.Pure(resTy) : Context.Fail<Ty>(ProcessError.ParameterTypeMismatch(Location))
                                select rt,

                            _ => Context.Fail<Ty>(ProcessError.FunctionTypeExpected(Location))
                        }
            select res;

    }

    public record TmLet(Loc Location, string Name, Term Value, Term Body) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmLet(Location, Name, Value.Subst(onVar, onType, name), Body.Subst(onVar, onType, name));
        
        public override Context<Term> Eval1 =>
            Value switch
            {
                var v1 when v1.IsVal => Context.Pure(Body.Subst(Name, v1)),
                _                    => from v in Value.Eval1
                                        select new TmLet(Location, Name, v, Body) as Term
            };

        public override Context<Ty> TypeOf =>
            from v in Value.TypeOf
            from r in Context.local(ctx => ctx.AddLocal(Name, new VarBind(v)),
                                    Body.TypeOf)
            select r;
    }

    public record TmFix(Loc Location, Term Term) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmFix(Location, Term.Subst(onVar, onType, name));

        public override Context<Term> Eval1 =>
            Term switch
            {
                TmLam              => Context.Pure(Term),
                var t when t.IsVal => Context.Fail<Term>(ProcessError.NoRuleApplies),
                _                  => from t in Term.Eval1
                                      select new TmFix(Location, t) as Term
            };

        public override Context<Ty> TypeOf =>
            from tyt1 in Term.TypeOf
            from simp in Context.simplifyTy(tyt1)
            from resu in simp switch
                         {
                             TyArr (var tyT11, var tyT12) =>
                                 from eq in tyT12.Equiv(tyT11)
                                 from rt in eq ? Context.Pure(tyT12) : Context.Fail<Ty>(ProcessError.BodyIncompatibleWithDomain(Location))
                                 select rt,
                             _ => Context.Fail<Ty>(ProcessError.FunctionTypeExpected(Location))
                         }
            select resu;
    }

    public record TmString(Loc Location, string Value) : Term(Location)
    {
        public override bool IsVal =>
            true;
    
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyString.Default);
    }

    public record TmInt(Loc Location, long Value) : Term(Location)
    {
        public override bool IsNumeric =>
            true;
    
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyInt.Default);
    }

    public record TmFloat(Loc Location, double Value) : Term(Location)
    {
        public override bool IsNumeric =>
            true;
    
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyFloat.Default);
    }

    public record TmProcessId(Loc Location, ProcessId Value) : Term(Location)
    {
        public override bool IsVal =>
            true;
    
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyProcessId.Default);
    }

    public record TmProcessName(Loc Location, ProcessName Value) : Term(Location)
    {
        public override bool IsVal =>
            true;
    
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyProcessName.Default);
    }

    public record TmProcessFlag(Loc Location, ProcessFlags Value) : Term(Location)
    {
        public override bool IsVal =>
            true;
    
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyProcessFlag.Default);
    }

    public record TmTime(Loc Location, Time Value) : Term(Location)
    {
        public override bool IsVal =>
            true;
    
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyTime.Default);
    }

    public record TmMessageDirective(Loc Location, MessageDirective Value) : Term(Location)
    {
        public override bool IsVal =>
            true;
    
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyMessageDirective.Default);
    }

    public record TmDirective(Loc Location, Directive Value) : Term(Location)
    {
        public override bool IsVal =>
            true;
    
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyDirective.Default);
    }

    public record TmUnit(Loc Location) : Term(Location)
    {
        public override bool IsVal =>
            true;
    
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(TyUnit.Default);
    }

    public record TmAscribe (Loc Location, Term Term, Ty Type) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmAscribe(Location, Term.Subst(onVar, onType, name), onType(name, Type));

        public override Context<Term> Eval1 =>
            Term switch
            {
                var v when v.IsVal => Context.Pure(v),
                _                  => from t in Term.Eval1
                                      select new TmAscribe(Location, t, Type) as Term
            };
        
        public override Context<Ty> TypeOf =>
            from t1 in Term.TypeOf
            from eq in t1.Equiv(Type)
            from rt in eq ? Context.Pure(Type) :  Context.Fail<Ty>(ProcessError.AscribeMismatch(Location))
            select rt;
    }

    public record Field(string Name, Term Value);
    
    public record TmRecord (Loc Location, Seq<Field> Fields) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmRecord(Location, Fields.Map(cs => new Field(cs.Name, cs.Value.Subst(onVar, onType, name))));

        public override bool IsVal =>
            Fields.ForAll(f => f.Value.IsVal);

        public override Context<Term> Eval1 =>
            from nm in Fields.ForAll(f => f.Value.IsVal)
                           ? Context.Fail<Unit>(ProcessError.NoRuleApplies)
                           : Context.Pure(unit)
            from fs in Fields.Sequence(f => f.Value.IsVal ? Context.Pure(f) : f.Value.Eval1.Map(v => new Field(f.Name, v)))
            select new TmRecord(Location, fs) as Term;

        public override Context<Ty> TypeOf =>
            from ftys in Fields.Sequence(f => f.Value.TypeOf.Map(ty => new FieldTy(f.Name, ty)))
            select new TyRecord(ftys) as Ty;
    }

    public record TmProj (Loc Location, Term Term, string Member) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmProj(Location, Term.Subst(onVar, onType, name), Member);

        public override Context<Term> Eval1 =>
            Term switch
            {
                TmRecord(_, var fields) {IsVal: true} =>
                    fields.Find(f => f.Name == Member).Case switch
                    {
                        Field f => Context.Pure(f.Value),
                        _       => Context.Fail<Term>(ProcessError.NoRuleApplies)
                    },

                _ => from t in Term.Eval1
                     select new TmProj(Location, t, Member) as Term
            };

        public override Context<Ty> TypeOf =>
            from ty in Term.TypeOf
            from st in Context.simplifyTy(ty)
            from rt in st switch
                       {
                           TyRecord (var fieldtys) =>
                               fieldtys.Find(f => f.Name == Member).Case switch
                               {
                                   FieldTy fty => Context.Pure(fty.Type),
                                   _           => Context.Fail<Ty>(ProcessError.FieldNotMemberOfType(Location, Member)),
                               },
                           _ => Context.Fail<Ty>(ProcessError.ExpectedRecordType(Location)),
                       }
            select rt;
    }

    public record TmInert (Loc Location, Ty Type) : Term(Location)
    {
        public override Term Subst(Func<Loc, string, string, Term> onVar, Func<string, Ty, Ty> onType, string name) =>
            new TmInert(Location, onType(name, Type));
     
        public override Context<Term> Eval1 =>
            Context.Fail<Term>(ProcessError.NoRuleApplies);

        public override Context<Ty> TypeOf =>
            Context.Pure(Type);
    }
}