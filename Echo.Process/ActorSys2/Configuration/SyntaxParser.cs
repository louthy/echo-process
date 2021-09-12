using System;
using System.Linq;
using System.Text;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Parsec;
using static LanguageExt.Prelude;
using static LanguageExt.Parsec.Char;
using static LanguageExt.Parsec.Expr;
using static LanguageExt.Parsec.Prim;
using static LanguageExt.Parsec.Token2;
using static LanguageExt.Parsec.Indent;
using LanguageExt.UnitsOfMeasure;

namespace Echo.ActorSys2.Configuration
{
    public static class SyntaxParser
    {
        public static Fin<Seq<Decl>> Parse(string source, string path)
        {
            Parser<Term>? term = null;
            Parser<Term>? expr = null;
            
            var opChars = ":!%&*+.<=>\\^|-~";

            var builtTypeNames = HashSet("int", "float", "bool", "process-id", "process-name", "process-flags", "time", 
                                         "directive", "message-directive", "disp", "cluster", "strategy", "router", "unit", "array");
 
            // Process config definition
            var def = GenLanguageDef.Empty.With(
                CommentStart: "/*",
                CommentEnd: "*/",
                CommentLine: "//",
                NestedComments: true,
                OpStart: oneOf("-+/*=!><|&%!~^"),
                OpLetter: oneOf(opChars),
                IdentStart: letter,
                IdentLetter: either(alphaNum, oneOf("-_")),
                ReservedNames: List("if", "then", "else", "match", "as", "let", "redirect", "when", "true", "false", "unit",
                                    "default", "listen-remote-and-local", "persist-all", "persist-inbox", "persist-state", "remote-publish", "remote-state-publish",
                                    "forward-to-self", "forward-to-parent", "forward-to-dead-letters", "stay-in-queue", "forward-to-process",
                                    "resume", "restart", "escalate", "stop",
                                    "cluster", "strategy", "router"
                ),
                ReservedOpNames: List("-", "+", "/", "*", "==", "!=", ">", "<", "<=", ">=", "||", "&&", "|", "&", "%", "!", "~", "^")
            );

            Loc mkLoc(Pos begin, Pos end) =>
                new Loc(path, begin, end);

            // Token parser
            // This builds the standard token parser from the definition above
            var lexer         = makeTokenParser(def);
            var identifier    = lexer.Identifier;
            var stringLiteral = lexer.StringLiteral;
            var integer       = lexer.Integer;
            var floating      = lexer.Float;
            var natural       = lexer.Natural;
            var whiteSpace    = lexer.WhiteSpace;
            var symbol        = lexer.Symbol;
            var keyword       = lexer.Reserved;
            var reservedOp    = lexer.ReservedOp;

            static Func<Term, Term, Term> BinaryOp(string op) =>
                (Term lhs, Term rhs) => Term.App(Term.App(Term.Var(lhs.Location, op), lhs), rhs);

            static Func<Term, Term> PrefixOp(string op) =>
                (Term rhs) => Term.App(Term.Var(rhs.Location, op), rhs);

            static Func<Term, Term> PostfixOp(string op) =>
                (Term lhs) => Term.App(Term.Var(lhs.Location, op), lhs);

            // Binary operator parser
            Operator<Term> Binary(string name, Assoc assoc) =>
                Operator.Infix(assoc, reservedOp(name).Map(_ => BinaryOp(name)));

            // Prefix operator parser
            Operator<Term> Prefix(string name) =>
                Operator.Prefix(reservedOp(name).Map(_ => PrefixOp(name)));

            // Postfix operator parser
            Operator<Term> Postfix(string name) =>
                Operator.Postfix(reservedOp(name).Map(_ => PostfixOp(name)));
            
            Parser<(A Value, Pos Begin, Pos End, int BeginIndex, int EndIndex)> token<A>(Parser<A> p) =>
                lexer.Lexeme(p);

            Parser<(A Value, Pos Begin, Pos End, int BeginIndex, int EndIndex)> brackets<A>(Parser<(A Value, Pos Begin, Pos End, int BeginIndex, int EndIndex)> p) =>
                lexer.Brackets(p);

            Parser<(A Value, Pos Begin, Pos End, int BeginIndex, int EndIndex)> parens<A>(Parser<(A Value, Pos Begin, Pos End, int BeginIndex, int EndIndex)> p) =>
                lexer.Parens(p);

            Parser<(Seq<A> Value, Pos Begin, Pos End, int BeginIndex, int EndIndex)> commaSep<A>(Parser<(A Value, Pos Begin, Pos End, int BeginIndex, int EndIndex)> p) =>
                lexer.CommaSep(p);

            Parser<(Seq<A> Value, Pos Begin, Pos End, int BeginIndex, int EndIndex)> commaSep1<A>(Parser<(A Value, Pos Begin, Pos End, int BeginIndex, int EndIndex)> p) =>
                lexer.CommaSep1(p);
            

            // Operator table
            Operator<Term>[][] operators =
            {
                new[] {Binary("||", Assoc.Left)},
                new[] {Binary("&&", Assoc.Left)},
                new[] {Binary("==", Assoc.None), Binary("!=", Assoc.None)},
                new[] {Binary("<", Assoc.None), Binary(">", Assoc.None), Binary(">=", Assoc.None), Binary("<=", Assoc.None)},
                new[] {Binary("+", Assoc.Left), Binary("-", Assoc.Left)},
                new[] {Binary("*", Assoc.Left), Binary("/", Assoc.Left), Binary("%", Assoc.Left)},
                new[] {Binary("&", Assoc.Left)},
                new[] {Binary("^", Assoc.Left)},
                new[] {Binary("|", Assoc.Left)},
                new[] {Prefix("-"), Prefix("+"), Prefix("!")},
            };

            // ProcessId parser
            var processId =
                token(
                    from xs in many1(choice(lower, digit, oneOf("@/[,-_]{}:")))
                    let r = (new string(xs.ToArray())).Trim()
                    let pid = ProcessId.TryParse(r)
                    from res in pid.Match(
                        Right: result,
                        Left: ex => failure<ProcessId>($"{ex.Message} '({r})'"))
                    select res);

            // ProcessName parser
            var processName =
                token(
                    from o in symbol("\"")
                    from xs in many1(choice(lower, digit, oneOf("@/[,-_]{.}:")))
                    from c in symbol("\"")
                    let r = (new string(xs.ToArray())).Trim()
                    let n = ProcessName.TryParse(r)
                    from res in n.Match(
                        Right: result,
                        Left: ex => failure<ProcessName>(ex.Message))
                    select res);

            // Let term
            var letTerm = from k in keyword("let")
                          from v in indented(from n in identifier
                                             from e in symbol(":")
                                             from v in expr
                                             select (Name: n.Value, Value: v))
                          from r in expr
                          select Term.Let(mkLoc(k.BeginPos, v.Value.Location.End), v.Name, v.Value, r);

            // Record term
            var recordTerm = from fs in many1(attempt(
                                                  indented2(
                                                      from nam in identifier
                                                      from col in symbol(":")
                                                      from val in expr
                                                      select (Name: nam, Value: val))))
                             select Term.Record(mkLoc(fs.Head.Name.BeginPos, fs.Last.Name.EndPos),
                                                fs.Map(f => new Field(f.Name.Value, f.Value)));

            // Array 
            var arrayTerm = from _  in result(unit) // expr is null if this isn't used
                            from xs in lexer.BracesCommaSep(expr.Expand())
                            select Term.Array(mkLoc(xs.BeginPos, xs.EndPos), xs.Value);

            var intTerm         = lexer.Integer.Map(x => Term.Int(mkLoc(x.BeginPos, x.EndPos), x.Value));
            var floatTerm       = lexer.Float.Map(x => Term.Float(mkLoc(x.BeginPos, x.EndPos), x.Value));
            var trueTerm        = lexer.Reserved("true").Map(x => Term.True(mkLoc(x.BeginPos, x.EndPos)));
            var falseTerm       = lexer.Reserved("false").Map(x => Term.False(mkLoc(x.BeginPos, x.EndPos)));
            var unitTerm        = lexer.Reserved("unit").Map(x => Term.Unit(mkLoc(x.BeginPos, x.EndPos)));
            var stringTerm      = lexer.StringLiteral.Map(x => Term.String(mkLoc(x.BeginPos, x.EndPos), x.Value));
            var processIdTerm   = processId.Map(pid => Term.ProcessId(mkLoc(pid.Begin, pid.End), pid.Value));
            var processNameTerm = processName.Map(pname => Term.ProcessName(mkLoc(pname.Begin, pname.End), pname.Value));
            var processFlagTerm = from f in choice(attempt(keyword("default")),
                                                   attempt(keyword("listen-remote-and-local")),
                                                   attempt(keyword("persist-all")),
                                                   attempt(keyword("persist-inbox")),
                                                   attempt(keyword("persist-state")),
                                                   attempt(keyword("remote-publish")),
                                                   keyword("remote-state-publish"))
                                  select Term.ProcessFlag(mkLoc(f.BeginPos, f.EndPos),
                                                          f.Value switch
                                                          {
                                                              "listen-remote-and-local" => ProcessFlags.ListenRemoteAndLocal,
                                                              "persist-all"             => ProcessFlags.PersistAll,
                                                              "persist-inbox"           => ProcessFlags.PersistInbox,
                                                              "persist-state"           => ProcessFlags.PersistState,
                                                              "remote-publish"          => ProcessFlags.RemotePublish,
                                                              "remote-state-publish"    => ProcessFlags.RemoteStatePublish,
                                                              _                         => ProcessFlags.Default
                                                          });
            var timeTerm = from tv in lexer.Integer
                           from un in choice(
                               attempt(keyword("seconds").Map(_ => tv.Value * seconds)),
                               attempt(keyword("second").Map(_ => tv.Value * seconds)),
                               attempt(keyword("secs").Map(_ => tv.Value * seconds)),
                               attempt(keyword("sec").Map(_ => tv.Value * seconds)),
                               attempt(keyword("s").Map(_ => tv.Value * seconds)),
                               attempt(keyword("minutes").Map(_ => tv.Value * minutes)),
                               attempt(keyword("minute").Map(_ => tv.Value * minutes)),
                               attempt(keyword("mins").Map(_ => tv.Value * minutes)),
                               attempt(keyword("min").Map(_ => tv.Value * minutes)),
                               attempt(keyword("milliseconds").Map(_ => tv.Value * milliseconds)),
                               attempt(keyword("millisecond").Map(_ => tv.Value * milliseconds)),
                               attempt(keyword("ms").Map(_ => tv.Value * milliseconds)),
                               attempt(keyword("hours").Map(_ => tv.Value * hours)),
                               attempt(keyword("hour").Map(_ => tv.Value * hours)),
                               keyword("hr").Map(_ => tv.Value * hours)).label("time unit")
                           select Term.Time(mkLoc(tv.BeginPos, tv.EndPos), un);

            var messageDirectiveTerm = choice(attempt(keyword("forward-to-self").Map(tm => Term.MessageDirective(mkLoc(tm.BeginPos, tm.EndPos), MessageDirective.ForwardToSelf))),
                                              attempt(keyword("forward-to-parent").Map(tm => Term.MessageDirective(mkLoc(tm.BeginPos, tm.EndPos), MessageDirective.ForwardToParent))),
                                              attempt(keyword("forward-to-dead-letters").Map(tm => Term.MessageDirective(mkLoc(tm.BeginPos, tm.EndPos), MessageDirective.ForwardToDeadLetters))),
                                              attempt(keyword("stay-in-queue").Map(tm => Term.MessageDirective(mkLoc(tm.BeginPos, tm.EndPos), MessageDirective.StayInQueue))),
                                              from tm in keyword("forward-to-process")
                                              from p in processId
                                              select Term.MessageDirective(mkLoc(tm.BeginPos, tm.EndPos), MessageDirective.ForwardTo(p.Value)));

            var directiveTerm = choice(attempt(keyword("resume").Map(tm => Term.InboxDirective(mkLoc(tm.BeginPos, tm.EndPos), Directive.Resume))),
                                       attempt(keyword("restart").Map(tm => Term.InboxDirective(mkLoc(tm.BeginPos, tm.EndPos), Directive.Restart))),
                                       attempt(keyword("stop").Map(tm => Term.InboxDirective(mkLoc(tm.BeginPos, tm.EndPos), Directive.Stop))),
                                       keyword("escalate").Map(tm => Term.InboxDirective(mkLoc(tm.BeginPos, tm.EndPos), Directive.Escalate)));
            
            var valueTerm = choice(
                                attempt(arrayTerm),
                                attempt(timeTerm),
                                attempt(floatTerm),
                                attempt(intTerm),
                                attempt(trueTerm),
                                attempt(falseTerm),
                                attempt(unitTerm),
                                attempt(processFlagTerm),
                                attempt(processIdTerm),
                                attempt(processNameTerm),
                                attempt(stringTerm),
                                attempt(messageDirectiveTerm),
                                attempt(directiveTerm),
                                recordTerm);

            var identTerm = identifier.Map(id => Term.Var(mkLoc(id.BeginPos, id.EndPos), id.Value));

            term = choice(attempt(letTerm),
                          attempt(valueTerm),
                          attempt(identTerm),
                          lazyp(() => lexer.Parens(expr.Expand()).Map(t => t.Value)));
            
            var expr1 = buildExpressionParser(operators, term);

            expr = lexer.CommaSep1(attempt(expr1.Expand()))
                        .Map(xs => xs.Value.Count == 1
                                       ? xs.Value.Head
                                       : Term.Tuple(xs.Value));

            var topLevelVarDecl = from k in keyword("let")
                                  from v in indented(from n in identifier
                                                     from e in symbol(":")
                                                     from v in expr
                                                     select (Name: n.Value, Value: v))
                                  select Decl.GlobalVar(mkLoc(k.BeginPos, v.Value.Location.End), v.Name, v.Value);

            var clusterDecl = from k in keyword("cluster")
                              from n in identifier
                              from _ in keyword("in")
                              from a in identifier
                              from c in symbol(":")
                              from r in recordTerm
                              select Decl.Cluster(mkLoc(k.BeginPos, r.Location.End), n.Value, a.Value, (TmRecord)r);

            var processDecl = from k in keyword("process")
                              from n in identifier
                              from c in symbol(":")
                              from r in recordTerm
                              select Decl.Process(mkLoc(k.BeginPos, r.Location.End), n.Value, (TmRecord)r);

            var routerDecl = from k in keyword("router")
                             from n in identifier
                             from c in symbol(":")
                             from r in recordTerm
                             select Decl.Router(mkLoc(k.BeginPos, r.Location.End), n.Value, (TmRecord)r);

            var strategyDecl = from k in keyword("strategy")
                               from n in identifier
                               from c in symbol(":")
                               from r in recordTerm
                               select Decl.Strategy(mkLoc(k.BeginPos, r.Location.End), n.Value, (TmRecord)r);
            
            var recordDecl = from k in keyword("record")
                             from n in identifier
                             from c in symbol(":")
                             from r in recordTerm
                             select Decl.Record(mkLoc(k.BeginPos, r.Location.End), n.Value, (TmRecord)r);

            var decls = many1(choice(topLevelVarDecl,
                                     clusterDecl,
                                     processDecl,
                                     routerDecl,
                                     strategyDecl,
                                     recordDecl));

            var sourceP = from _1 in lexer.WhiteSpace
                          from ds in decls
                          from _2 in eof
                          select ds;

            return parse(sourceP, source.ToPString())
                    .ToEither()
                    .Match(Right: FinSucc,
                           Left: e => FinFail<Seq<Decl>>(Error.New(e)));
        }

        static Parser<(Term Value, Pos BeginPos, Pos EndPos, int BeginIndex, int EndIndex)> Expand(this Parser<Term> p) =>
            p.Map(t => (t, t.Location.Begin, t.Location.End, 0, 0));

        /*
        static (Pos Begin, string Text) SplitDeclarations(string source)
        {
            var ranges    = new System.Collections.Generic.List<(Pos Begin, string Text)>();
            var column    = 0;
            var line      = 0;
            var start     = 0;
            var startLine = 0;
            var current   = 0;
            
            foreach (var ch in source)
            {
                if (column == 0 && current > start && !char.IsWhiteSpace(ch))
                {
                    ranges.Add((new Pos(startLine, 0), source.Substring(start, current - start)));
                    startLine = line;
                    start     = current;
                }

                if (ch == '\n')
                {
                    line++;
                    column = 0;
                }
                else
                {
                    column++;
                }
                current++;
            }
        }*/
    }
}