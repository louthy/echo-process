using System;
using Echo.ActorSys2.Configuration;
using LanguageExt.UnitsOfMeasure;
using static LanguageExt.Prelude;
using Xunit;

namespace Echo.Tests
{
    public class ConfLangTests
    {
        static string general = @"
-- type Person = { name    : string
--               , surname : string
--               , age     : int } 
-- 
-- type State s a : s → { value: a, state: s, faulted: Bool }    let State = ∀ s. s → ∀ a. a → Bool → { value : a, state : s, faulted : Bool  }

let State (value : a, faulted: Bool, state : s) =
    { state = state
    , value = value
    , faulted = faulted }

let return (value : a, state : s) = 
    State value false state

let applBool (f : Bool → Bool, x : Bool) = 
    if f x
    then true
    else false

let result = applBool ((x : Bool) => !x) true

let identity (x : a) =
    x

let test = identity 100      

let example = 10

cluster root as app = { node-name  = ""THE-BEAST""        -- Should match the web-site host-name unless the host-name is localhost, then it uses System.Environment.MachineName
                      , role       =  ""owin-web-role""
                      , connection = ""localhost""
                      , database   = ""0"" }

strategy strat one-for-one = 
     { backoff = (min = 1 seconds, max = 100 seconds, scalar = 2 * example) }

process echo = { pid = /root/user/echo
               , strategy = strat }
"; 
        
        [Fact]
        public void EndToEnd_Test()
        {
            // PARSE
            
            var fres  = SyntaxParser.Parse(general, "general.conf");
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 10);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);
            Assert.True(decls[2] is DeclGlobalVar);
            Assert.True(decls[3] is DeclGlobalVar);
            Assert.True(decls[4] is DeclGlobalVar);
            Assert.True(decls[5] is DeclGlobalVar);
            Assert.True(decls[6] is DeclGlobalVar);
            Assert.True(decls[7] is DeclCluster);
            Assert.True(decls[8] is DeclStrategy);
            Assert.True(decls[9] is DeclProcess);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("test").Case is TmAbbBind vb0 && vb0.Type.Case is TyInt && 
                        vb0.Term is TmInt tmInt && tmInt.Value == 100); 

            Assert.True(ctx.Context.TopBindings.Find("result").Case is TmAbbBind vbr && vbr.Type.Case is TyBool && 
                        vbr.Term is TmFalse); 

            Assert.True(ctx.Context.TopBindings.Find("app").Case is TmAbbBind vb1 && vb1.Type.Case is TyCluster cluster &&
                        cluster.Value.Fields.Find(f => f.Name == "node-name").Case is FieldTy fty1 && fty1.Type is TyString &&
                        cluster.Value.Fields.Find(f => f.Name == "role").Case is FieldTy fty2 && fty2.Type is TyString &&
                        cluster.Value.Fields.Find(f => f.Name == "connection").Case is FieldTy fty3 && fty3.Type is TyString &&
                        cluster.Value.Fields.Find(f => f.Name == "database").Case is FieldTy fty4 && fty4.Type is TyString);

            Assert.True(ctx.Context.TopBindings.Find("strat").Case is TmAbbBind vb2 && vb2.Type.Case is TyStrategy strategy &&
                        strategy.Type == StrategyType.OneForOne &&
                        strategy.Value.Fields.Find(f => f.Name == "backoff").Case is FieldTy fty5 && fty5.Type is TyTuple tuple &&
                        tuple.Types.Count == 3 &&
                        tuple.Types[0] is TyTime &&
                        tuple.Types[1] is TyTime &&
                        tuple.Types[2] is TyInt);

            Assert.True(ctx.Context.TopBindings.Find("echo").Case is TmAbbBind vb3 && vb3.Type.Case is TyProcess process &&
                        process.Value.Fields.Find(f => f.Name == "pid").Case is FieldTy fty6 && fty6.Type is TyProcessId &&
                        process.Value.Fields.Find(f => f.Name == "strategy").Case is FieldTy fty7 && fty7.Type is TyRecord);
        }

        [Fact]
        public void Lambda_IdentityTest()
        {
            // PARSE
            
            var fres  = SyntaxParser.Parse(@"
let identity (x : a) = x

let test1 = identity 100
let test2 = identity ""Hello, World""
let test3 = identity ((x : Int) => x + 1)

", "general.conf");
            
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 4);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);
            Assert.True(decls[2] is DeclGlobalVar);
            Assert.True(decls[3] is DeclGlobalVar);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("test1").Case is TmAbbBind vb0 && vb0.Type.Case is TyInt && 
                        vb0.Term is TmInt tmInt && tmInt.Value == 100); 

            Assert.True(ctx.Context.TopBindings.Find("test2").Case is TmAbbBind vb1 && vb1.Type.Case is TyString && 
                        vb1.Term is TmString tmStr && tmStr.Value == "Hello, World"); 

            Assert.True(ctx.Context.TopBindings.Find("test3").Case is TmAbbBind vb2 && 
                        vb2.Type.Case is TyArr arr &&
                        arr.X is TyInt && arr.Y is TyInt &&
                        vb2.Term is TmLam tmLam &&
                        tmLam.Name == "x" &&
                        tmLam.Type == Ty.Int &&
                        tmLam.Body is TmAdd tmAdd &&
                        tmAdd.Left is TmVar tmVar && tmVar.Name == "x" &&
                        tmAdd.Right is TmInt tmInt1 && tmInt1.Value == 1
                        ); 
        }
        
        [Fact]
        public void Lambda_PartialApplyConcrete()
        {
            // PARSE
            
            var fres = SyntaxParser.Parse(@"
let add (x : Int, y : Int) = x + y

let add1 = add 1
let test2 = add1 1
let test4 = add 2 2

", "general.conf");
            
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 4);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);
            Assert.True(decls[2] is DeclGlobalVar);
            Assert.True(decls[3] is DeclGlobalVar);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("add1").Case is TmAbbBind vb2 &&
                        vb2.Type.Case is TyArr arr &&
                        arr.X is TyInt && arr.Y is TyInt &&
                        vb2.Term is TmLam tmLam &&
                        tmLam.Name == "y" &&
                        tmLam.Type == Ty.Int &&
                        tmLam.Body is TmAdd tmAdd &&
                        tmAdd.Left is TmInt tmInt1 && tmInt1.Value == 1 &&
                        tmAdd.Right is TmVar tmVarY && tmVarY.Name == "y");

            Assert.True(ctx.Context.TopBindings.Find("test2").Case is TmAbbBind vbX && vbX.Type.Case is TyInt && 
                        vbX.Term is TmInt tmIntX && tmIntX.Value == 2); 

            Assert.True(ctx.Context.TopBindings.Find("test4").Case is TmAbbBind vb1 && vb1.Type.Case is TyInt && 
                        vb1.Term is TmInt tmInt && tmInt.Value == 4); 
        }
        
        [Fact]
        public void Lambda_PartialApplyAbstract()
        {
            // PARSE
            
            var fres = SyntaxParser.Parse(@"
let f (x : a, y : a) = x

let f1 = f 1
let xr = f1 0

let f2 = f ""Hello, World""
let yr = f2 """"

", "general.conf");
            
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 5);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);
            Assert.True(decls[2] is DeclGlobalVar);
            Assert.True(decls[3] is DeclGlobalVar);
            Assert.True(decls[4] is DeclGlobalVar);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("f1").Case is TmAbbBind vb2 &&
                        vb2.Type.Case is TyArr arr &&
                        arr.X is TyInt && arr.Y is TyInt &&
                        vb2.Term is TmLam tmLam &&
                        tmLam.Name == "y" &&
                        tmLam.Type == Ty.Int &&
                        tmLam.Body is TmInt tmInt0 && tmInt0.Value == 1);

            Assert.True(ctx.Context.TopBindings.Find("xr").Case is TmAbbBind vbX && vbX.Type.Case is TyInt && 
                        vbX.Term is TmInt tmIntX && tmIntX.Value == 1); 

            Assert.True(ctx.Context.TopBindings.Find("f2").Case is TmAbbBind vb3 &&
                        vb3.Type.Case is TyArr arr1 &&
                        arr1.X is TyString && arr1.Y is TyString &&
                        vb3.Term is TmLam tmLam1 &&
                        tmLam1.Name == "y" &&
                        tmLam1.Type == Ty.String &&
                        tmLam1.Body is TmString tmStr && tmStr.Value == "Hello, World");

            Assert.True(ctx.Context.TopBindings.Find("yr").Case is TmAbbBind vbY && vbY.Type.Case is TyString && 
                        vbY.Term is TmString tmStrY && tmStrY.Value == "Hello, World"); 
        }
        
        
        [Fact]
        public void Lambda_PartialApplyAbstract2()
        {
            // PARSE
            
            var fres = SyntaxParser.Parse(@"
let f (x : a) = 
    (y : a) => 
        x

let f1 = f 1
let xr = f1 0

let f2 = f ""Hello, World""
let yr = f2 """"

", "general.conf");
            
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 5);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);
            Assert.True(decls[2] is DeclGlobalVar);
            Assert.True(decls[3] is DeclGlobalVar);
            Assert.True(decls[4] is DeclGlobalVar);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("f1").Case is TmAbbBind vb2 &&
                        vb2.Type.Case is TyArr arr &&
                        arr.X is TyInt && arr.Y is TyInt &&
                        vb2.Term is TmLam tmLam &&
                        tmLam.Name == "y" &&
                        tmLam.Type == Ty.Int &&
                        tmLam.Body is TmInt tmInt0 && tmInt0.Value == 1);

            Assert.True(ctx.Context.TopBindings.Find("xr").Case is TmAbbBind vbX && vbX.Type.Case is TyInt && 
                        vbX.Term is TmInt tmIntX && tmIntX.Value == 1); 

            Assert.True(ctx.Context.TopBindings.Find("f2").Case is TmAbbBind vb3 &&
                        vb3.Type.Case is TyArr arr1 &&
                        arr1.X is TyString && arr1.Y is TyString &&
                        vb3.Term is TmLam tmLam1 &&
                        tmLam1.Name == "y" &&
                        tmLam1.Type == Ty.String &&
                        tmLam1.Body is TmString tmStr && tmStr.Value == "Hello, World");

            Assert.True(ctx.Context.TopBindings.Find("yr").Case is TmAbbBind vbY && vbY.Type.Case is TyString && 
                        vbY.Term is TmString tmStrY && tmStrY.Value == "Hello, World"); 
        }
                     
        [Fact]
        public void Lambda_PartialApplyAbstract3()
        {
            // PARSE
            
            var fres = SyntaxParser.Parse(@"
let f = (x : a) => 
            (y : a) => 
                x

let f1 = f 1
let xr = f1 0

let f2 = f ""Hello, World""
let yr = f2 """"

", "general.conf");
            
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 5);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);
            Assert.True(decls[2] is DeclGlobalVar);
            Assert.True(decls[3] is DeclGlobalVar);
            Assert.True(decls[4] is DeclGlobalVar);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("f1").Case is TmAbbBind vb2 &&
                        vb2.Type.Case is TyArr arr &&
                        arr.X is TyInt && arr.Y is TyInt &&
                        vb2.Term is TmLam tmLam &&
                        tmLam.Name == "y" &&
                        tmLam.Type == Ty.Int &&
                        tmLam.Body is TmInt tmInt0 && tmInt0.Value == 1);

            Assert.True(ctx.Context.TopBindings.Find("xr").Case is TmAbbBind vbX && vbX.Type.Case is TyInt && 
                        vbX.Term is TmInt tmIntX && tmIntX.Value == 1); 

            Assert.True(ctx.Context.TopBindings.Find("f2").Case is TmAbbBind vb3 &&
                        vb3.Type.Case is TyArr arr1 &&
                        arr1.X is TyString && arr1.Y is TyString &&
                        vb3.Term is TmLam tmLam1 &&
                        tmLam1.Name == "y" &&
                        tmLam1.Type == Ty.String &&
                        tmLam1.Body is TmString tmStr && tmStr.Value == "Hello, World");

            Assert.True(ctx.Context.TopBindings.Find("yr").Case is TmAbbBind vbY && vbY.Type.Case is TyString && 
                        vbY.Term is TmString tmStrY && tmStrY.Value == "Hello, World"); 
        }   
                             
        [Fact]
        public void Lambda_PartialApplyAbstract4()
        {
            // PARSE
            
            var fres = SyntaxParser.Parse(@"
let f = (x : a) => 
            (y : b) => 
                x

let f1 = f 1
let xr = f1 true

let f2 = f ""Hello, World""
let yr = f2 false

", "general.conf");
            
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 5);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);
            Assert.True(decls[2] is DeclGlobalVar);
            Assert.True(decls[3] is DeclGlobalVar);
            Assert.True(decls[4] is DeclGlobalVar);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("f1").Case is TmAbbBind vb2 &&
                        vb2.Type.Case is TyAll all && all.Type is TyArr arr &&
                        arr.X is TyVar tv1 && tv1.Name == "b" &&
                        arr.Y is TyInt && 
                        vb2.Term is TmTLam tmLiftLam &&
                        tmLiftLam.Expr is TmLam tmLam &&
                        tmLam.Name == "y" &&
                        tmLam.Type is TyVar tvr && tvr.Name == "b" &&
                        tmLam.Body is TmInt tmInt0 && tmInt0.Value == 1);

            Assert.True(ctx.Context.TopBindings.Find("xr").Case is TmAbbBind vbX && vbX.Type.Case is TyInt && 
                        vbX.Term is TmInt tmIntX && tmIntX.Value == 1); 

            Assert.True(ctx.Context.TopBindings.Find("f2").Case is TmAbbBind vb3 &&
                        vb3.Type.Case is TyAll all1 && all1.Type is TyArr arr1 &&
                        arr1.X is TyVar tv2 && tv2.Name == "b" &&
                        arr1.Y is TyString && 
                        vb3.Term is TmTLam tmLiftLam2 &&
                        tmLiftLam2.Expr is TmLam tmLam2 &&
                        tmLam2.Name == "y" &&
                        tmLam2.Type is TyVar tvr1 && tvr1.Name == "b" &&
                        tmLam2.Body is TmString tmStr && tmStr.Value == "Hello, World");

            Assert.True(ctx.Context.TopBindings.Find("yr").Case is TmAbbBind vbY && vbY.Type.Case is TyString && 
                        vbY.Term is TmString tmStrY && tmStrY.Value == "Hello, World"); 
        }   
                                     
        [Fact]
        public void ApplyGenericToGeneric()
        {
            // PARSE
            
            var fres = SyntaxParser.Parse(@"
let f1 (x : b) = x
let f2 (x : a) = f1 x
let x  = f2 100

", "general.conf");
            
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 3);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);
            Assert.True(decls[2] is DeclGlobalVar);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("f1").Case is TmAbbBind vb1 && 
                        vb1.Type.Case is TyAll all && all.Type is TyArr tyarr && 
                        tyarr.X is TyVar tvarA && tvarA.Name == "b" &&
                        tyarr.Y is TyVar tvarB && tvarB.Name == "b" &&
                        vb1.Term is TmTLam ttlam &&  
                        ttlam.Subject == "b" && ttlam.Expr is TmLam tlam &&
                        tlam.Name == "x" && tlam.Body is TmVar tmvar && 
                        tmvar.Name == "x");


            Assert.True(ctx.Context.TopBindings.Find("f2").Case is TmAbbBind vb2 && 
                        vb2.Type.Case is TyAll all2 && all2.Type is TyArr tyarr2 && 
                        tyarr2.X is TyVar tvarA2 && tvarA2.Name == "a" &&
                        tyarr2.Y is TyVar tvarB2 && tvarB2.Name == "a" &&
                        vb2.Term is TmTLam ttlam2 &&  
                        ttlam2.Subject == "a" && ttlam2.Expr is TmLam tlam2 &&
                        tlam2.Name == "x" );

            Assert.True(ctx.Context.TopBindings.Find("x").Case is TmAbbBind vb3 &&
                        vb3.Term is TmInt v && v.Value == 100);
        }
                                             
        [Fact]
        public void GenericRecordConstruction()
        {
            // PARSE
            
            var fres = SyntaxParser.Parse(@"
let f (x : b, y : Bool) = { x = x, y = y }
let x  = f 100 true

", "general.conf");
            
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 2);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("x").Case is TmAbbBind vb &&
                        vb.Type.Case is TyRecord rec && rec.Fields.Count == 2 &&
                        rec.Fields[0].Name == "x" && rec.Fields[0].Type is TyInt &&
                        rec.Fields[1].Name == "y" && rec.Fields[1].Type is TyBool &&
                        vb.Term is TmRecord tmrec && tmrec.Fields.Count == 2 &&
                        tmrec.Fields[0].Name == "x" && tmrec.Fields[0].Value is TmInt x && x.Value == 100 &&
                        tmrec.Fields[1].Name == "y" && tmrec.Fields[1].Value is TmTrue);

        }
                                                     
        [Fact]
        public void GenericRecordConstruction2()
        {
            // PARSE
            
            var fres = SyntaxParser.Parse(@"
let f (x : a, y : Bool, z : b) = { x = x, y = y, z = z }

let x  = f 100 true ""Hello, World""

", "general.conf");
            
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 2);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("x").Case is TmAbbBind vb &&
                        vb.Type.Case is TyRecord rec && rec.Fields.Count == 3 &&
                        rec.Fields[0].Name == "x" && rec.Fields[0].Type is TyInt &&
                        rec.Fields[1].Name == "y" && rec.Fields[1].Type is TyBool &&
                        rec.Fields[2].Name == "z" && rec.Fields[2].Type is TyString &&
                        vb.Term is TmRecord tmrec && tmrec.Fields.Count == 3 &&
                        tmrec.Fields[0].Name == "x" && tmrec.Fields[0].Value is TmInt x && x.Value == 100 &&
                        tmrec.Fields[1].Name == "y" && tmrec.Fields[1].Value is TmTrue &&
                        tmrec.Fields[2].Name == "z" && tmrec.Fields[2].Value is TmString z && z.Value == "Hello, World"
                        );

        }
                                                             
        [Fact]
        public void GenericRecordConstruction3()
        {
            // PARSE
            
            var fres = SyntaxParser.Parse(@"
let f (x : a, y : Bool, z : b) = { x = x, y = y, z = z }

let g (one : d, two : e) = f one true two  

let x  = g 100 ""Hello, World""

", "general.conf");
            
            var decls = fres.ThrowIfFail();

            Assert.True(decls.Count == 3);
            Assert.True(decls[0] is DeclGlobalVar);
            Assert.True(decls[1] is DeclGlobalVar);
            Assert.True(decls[2] is DeclGlobalVar);

            // TYPE-CHECK
            
            var fctx = TypeChecker.Decls(decls).Run(Context.Empty);
            var ctx  = fctx.ThrowIfFail();

            Assert.True(ctx.Context.TopBindings.Find("x").Case is TmAbbBind vb &&
                        vb.Type.Case is TyRecord rec && rec.Fields.Count == 3 &&
                        rec.Fields[0].Name == "x" && rec.Fields[0].Type is TyInt &&
                        rec.Fields[1].Name == "y" && rec.Fields[1].Type is TyBool &&
                        rec.Fields[2].Name == "z" && rec.Fields[2].Type is TyString &&
                        vb.Term is TmRecord tmrec && tmrec.Fields.Count == 3 &&
                        tmrec.Fields[0].Name == "x" && tmrec.Fields[0].Value is TmInt x && x.Value == 100 &&
                        tmrec.Fields[1].Name == "y" && tmrec.Fields[1].Value is TmTrue &&
                        tmrec.Fields[2].Name == "z" && tmrec.Fields[2].Value is TmString z && z.Value == "Hello, World"
            );

        }
        
        [Fact]
        public void TopLevelLet_LabeledTuple()
        {
            var fres = SyntaxParser.Parse($"let x = (min = 1 seconds, max = 100 seconds, scalar = 2)", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmTuple val &&
                        val.Values.Count == 3 &&
                        val.Values[0] is TmNamed n0 && n0.Name == "min" && n0.Expr is TmTime t && t.Value == 1*second &&
                        val.Values[1] is TmNamed n1 && n1.Name == "max" && n1.Expr is TmTime m && m.Value == 100*seconds &&
                        val.Values[2] is TmNamed n2 && n2.Name == "scalar" && n2.Expr is TmInt s && s.Value == 2
                        );
        }
        

        [Theory]
        [InlineData("x")]
        [InlineData("X")]
        [InlineData("foo")]
        [InlineData("Foo")]
        [InlineData("foo1")]
        [InlineData("Foo1")]
        [InlineData("foo_1")]
        [InlineData("Foo-1")]
        public void TopLevelLet_Ident(string name)
        {
            var fres = SyntaxParser.Parse($"let x = {name}", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmVar val &&
                        val.Name == name);
        }
        
        [Theory]
        [InlineData(123)]
        [InlineData(0)]
        [InlineData(-123)]
        [InlineData(1)]
        [InlineData(long.MaxValue)]
        [InlineData(long.MinValue)]
        public void TopLevelLet_Int(long value)
        {
            var fres = SyntaxParser.Parse($"let x = {value}", "test.conf");
            var res = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmInt val &&
                        val.Value == value);
        }
        
        [Theory]
        [InlineData("", "")]
        [InlineData("Hello, World", "Hello, World")]
        [InlineData("Hello\\nWorld", "Hello\nWorld")]
        [InlineData("Hello\\rWorld", "Hello\rWorld")]
        [InlineData("Hello\\\"World", "Hello\"World")]
        public void TopLevelLet_String(string input, string output)
        {
            var fres = SyntaxParser.Parse($"let x = \"{input}\"", "test.conf");
            var res = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmString val &&
                        val.Value == output);
        }
        
        [Fact]
        public void TopLevelLet_True()
        {
            var fres = SyntaxParser.Parse("let x = true", "test.conf");
            var res = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmTrue);
        }
        
        [Fact]
        public void TopLevelLet_False()
        {
            var fres = SyntaxParser.Parse("let x = false", "test.conf");
            var res = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmFalse);
        }
        
        [Fact]
        public void TopLevelLet_Unit()
        {
            var fres = SyntaxParser.Parse("let x = unit", "test.conf");
            var res = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmUnit);
        }
        
        [Theory]
        [InlineData("default", ProcessFlags.Default)]
        [InlineData("listen-remote-and-local", ProcessFlags.ListenRemoteAndLocal)]
        [InlineData("persist-all", ProcessFlags.PersistAll)]
        [InlineData("persist-inbox", ProcessFlags.PersistInbox)]
        [InlineData("persist-state", ProcessFlags.PersistState)]
        [InlineData("remote-publish", ProcessFlags.RemotePublish)]
        [InlineData("remote-state-publish", ProcessFlags.RemoteStatePublish)]
        public void TopLevelLet_ProcessFlag(string input, ProcessFlags expected)
        {
            var fres = SyntaxParser.Parse($"let x = {input}", "test.conf");
            var res = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmProcessFlag flag &&
                        flag.Value == expected);
        }
        
        [Theory]
        //[InlineData("@role-name")]
        [InlineData("//root/123")]
        [InlineData("/sub")]
        [InlineData("/sub/node")]
        [InlineData("/sub/node-value")]
        [InlineData("//sub/node-value")]
        [InlineData("//sub/node-value/child-1023")]
        public void TopLevelLet_ProcessId(string input)
        {
            var fres = SyntaxParser.Parse($"let x = {input}", "test.conf");
            var res = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmProcessId pid &&
                        pid.Value == new ProcessId(input));
        }
                
        [Theory]
        [InlineData("[//sub/node-value/child-1023,//sub/node-value,/sub/node-value,/sub/node,/sub,//root/123]")]
        public void TopLevelLet_ProcessIdArray(string input)
        {
            var fres = SyntaxParser.Parse($"let x = {input}", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmArray val &&
                        val.Values.ForAll(v => v is TmProcessId)
                        );
        }
         
        [Theory]
        [InlineData("123.5")]
        [InlineData("100.0")]
        [InlineData("0.0")]
        [InlineData("-123.5")]
        [InlineData("-100.0")]
        [InlineData("1.5")]
        public void TopLevelLet_Float(string value)
        {
            var fres = SyntaxParser.Parse($"let x = {value}", "test.conf");
            var res  = fres.ThrowIfFail();

            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmFloat val &&
                        val.Value == double.Parse(value));
        }       
         
        [Theory]
        [InlineData("123 s", 1000)]
        [InlineData("100 sec", 1000)]
        [InlineData("100 secs", 1000)]
        [InlineData("0 second", 1000)]
        [InlineData("123 seconds", 1000)]
        [InlineData("123 min", 60000)]
        [InlineData("100 mins", 60000)]
        [InlineData("100 minute", 60000)]
        [InlineData("0 minutes", 60000)]
        [InlineData("123 ms", 1)]
        [InlineData("100 millisecond", 1)]
        [InlineData("100 milliseconds", 1)]
        [InlineData("123 hours", 60000*60)]
        [InlineData("100 hour", 60000*60)]
        [InlineData("100 hr", 60000*60)]
        public void TopLevelLet_Time(string value, int scalar)
        {
            var parts    = value.Split(' ');
            var expected = int.Parse(parts[0]) * scalar;
            
            var fres = SyntaxParser.Parse($"let x = {value}", "test.conf");
            var res  = fres.ThrowIfFail();

            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmTime val &&
                        (int)val.Value.Milliseconds == expected);
        }

        [Fact]
        public void TopLevelLet_MessageDirective_ForwardToSelf()
        {
            var fres = SyntaxParser.Parse($"let x = forward-to-self", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmMessageDirective flag &&
                        flag.Value == MessageDirective.ForwardToSelf);
        }

        [Fact]
        public void TopLevelLet_MessageDirective_ForwardToDeadLetters()
        {
            var fres = SyntaxParser.Parse($"let x = forward-to-dead-letters", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmMessageDirective flag &&
                        flag.Value == MessageDirective.ForwardToDeadLetters);
        }

        [Fact]
        public void TopLevelLet_MessageDirective_StayInQueue()
        {
            var fres = SyntaxParser.Parse($"let x = stay-in-queue", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmMessageDirective flag &&
                        flag.Value == MessageDirective.StayInQueue);
        }

        [Theory]
        [InlineData("//root/123")]
        [InlineData("/sub")]
        [InlineData("/sub/node")]
        [InlineData("/sub/node-value")]
        [InlineData("//sub/node-value")]
        [InlineData("//sub/node-value/child-1023")]
        public void TopLevelLet_MessageDirective_ForwardToProcess(string input)
        {
            var fres = SyntaxParser.Parse($"let x = forward-to-process {input}", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmMessageDirective flag &&
                        flag.Value == MessageDirective.ForwardTo(input));
        }
 
        [Fact]
        public void TopLevelLet_Directive_Resume()
        {
            var fres = SyntaxParser.Parse($"let x = resume", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmDirective flag &&
                        flag.Value == Directive.Resume);
        }   
 
        [Fact]
        public void TopLevelLet_Directive_Restart()
        {
            var fres = SyntaxParser.Parse($"let x = restart", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmDirective flag &&
                        flag.Value == Directive.Restart);
        }   
 
        [Fact]
        public void TopLevelLet_Directive_Escalate()
        {
            var fres = SyntaxParser.Parse($"let x = escalate", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmDirective flag &&
                        flag.Value == Directive.Escalate);
        }   
 
        [Fact]
        public void TopLevelLet_Directive_Stop()
        {
            var fres = SyntaxParser.Parse($"let x = stop", "test.conf");
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmDirective flag &&
                        flag.Value == Directive.Stop);
        }   
 
        [Fact]
        public void TopLevelLet_Record()
        {
            var fres = SyntaxParser.Parse(@"
let x = { id        = //root/user/test/123
        , name      = ""Paul""
        , surname   = ""Louth""
        , score     = 1000
        , dir       = resume
        , mdir      = forward-to-dead-letters }", 
                                          "test.conf");
            
            var res  = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmRecord record &&
                        record.Fields.Count == 6 &&
                        record.Fields[0].Name == "id" && record.Fields[0].Value is TmProcessId id &&  
                        record.Fields[1].Name == "name" && record.Fields[1].Value is TmString name && name.Value == "Paul" &&  
                        record.Fields[2].Name == "surname" && record.Fields[2].Value is TmString surname && surname.Value == "Louth" &&  
                        record.Fields[3].Name == "score" && record.Fields[3].Value is TmInt score && score.Value == 1000 &&  
                        record.Fields[4].Name == "dir" && record.Fields[4].Value is TmDirective dir && dir.Value == Directive.Resume &&
                        record.Fields[5].Name == "mdir" && record.Fields[5].Value is TmMessageDirective mdir && mdir .Value == MessageDirective.ForwardToDeadLetters   
                        );
        }   
    }
}