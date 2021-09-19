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
cluster root as app:
    node-name:   ""THE-BEAST""        -- Should match the web-site host-name unless the host-name is localhost, then it uses System.Environment.MachineName
    role:	     ""owin-web-role""
    connection:  ""localhost""
    database:    ""0""

strategy strat: 
    one-for-one:
        backoff: min = 1 seconds, max = 100 seconds, scalar = 2

process echo:
    pid: /root/user/echo
    strategy: strat
"; 
        
        [Fact]
        public void Test()
        {
            var fres = SyntaxParser.Parse(general, "general.conf");
            var res  = fres.ThrowIfFail();

            Assert.True(res.Count == 3);
            Assert.True(res[0] is DeclCluster);
            Assert.True(res[1] is DeclStrategy);
            Assert.True(res[2] is DeclProcess);
        }

        [Fact]
        public void TopLevelLet_LabeledTuple()
        {
            var fres = SyntaxParser.Parse($"let x: min = 1 seconds, max = 100 seconds, scalar = 2", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: {name}", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: {value}", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: \"{input}\"", "test.conf");
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
            var fres = SyntaxParser.Parse("let x: true", "test.conf");
            var res = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmTrue);
        }
        
        [Fact]
        public void TopLevelLet_False()
        {
            var fres = SyntaxParser.Parse("let x: false", "test.conf");
            var res = fres.ThrowIfFail();
            
            Assert.True(res.Count == 1);
            Assert.True(res.Head is DeclGlobalVar decl &&
                        decl.Name == "x" &&
                        decl.Value is TmFalse);
        }
        
        [Fact]
        public void TopLevelLet_Unit()
        {
            var fres = SyntaxParser.Parse("let x: unit", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: {input}", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: {input}", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: {input}", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: {value}", "test.conf");
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
            
            var fres = SyntaxParser.Parse($"let x: {value}", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: forward-to-self", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: forward-to-dead-letters", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: stay-in-queue", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: forward-to-process {input}", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: resume", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: restart", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: escalate", "test.conf");
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
            var fres = SyntaxParser.Parse($"let x: stop", "test.conf");
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
            var fres = SyntaxParser.Parse($@"
let x: record
         id:      //root/user/test/123
         name:    ""Paul""
         surname: ""Louth""
         score:   1000
         dir:     resume
         mdir:    forward-to-dead-letters", 
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