using System;
using System.Collections.Generic;
using System.Text;
using static LanguageExt.Prelude;
using static Echo.Process;
using LanguageExt;
using Echo.Client;

namespace Echo.Client
{
    class BarParse
    {
        const string header = "procsys:";
        public readonly string Source;
        public readonly int Length;
        public int Pos;

        StringBuilder sb = new StringBuilder();

        public BarParse(string source)
        {
            Source = source;
            Length = Source.Length;
        }

        public Either<string, Unit> Header()
        {
            Pos = 0;
            if (!Source.StartsWith(header)) return "Invalid request";
            Pos += header.Length;
            return unit;
        }

        public string GetNextText()
        {
            sb.Length = 0;

            for (; Pos < Length; Pos++)
            {
                var c = Source[Pos];
                if (c == '|')
                {
                    Pos++;
                    break;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        public string GetRemainingText()
        {
            var pos = Pos;
            Pos = Source.Length;
            return Source.Substring(pos);
        }

        public Either<string, string> GetRemaining()
        {
            var res = GetNextText();
            return String.IsNullOrEmpty(res)
                ? Left<string, string>("Empty field")
                : Right<string, string>(res);
        }

        public Either<string, string> GetNext()
        {
            var res = GetNextText();
            return String.IsNullOrEmpty(res)
                ? Left<string, string>("Empty field")
                : Right<string, string>(res);
        }

        public Either<string, long> GetLong() =>
            from x in GetNext()
            from y in parseLong(x).ToEither("Invalid long int")
            select y;

        public Either<string, ProcessId> GetProcessId() =>
            from x in GetNext()
            from y in FixupPID(x)
            select y;

        public Either<string, ClientMessageId> GetMessageId() =>
            GetLong().Map(ClientMessageId.New);

        public Either<string, ClientConnectionId> GetConnectionId(string remoteIp) =>
            GetNext()
                .Map(ClientConnectionId.New)
                .Bind(id => id.Value.StartsWith(remoteIp.GetHashCode().ToString() + "-")
                    ? Right<string, ClientConnectionId>(id)
                    : Left<string, ClientConnectionId>("Invalid client ID"));

        Either<string, ProcessId> FixupPID(string pidStr) =>
            String.IsNullOrWhiteSpace(pidStr) || pidStr == "/no-sender"
                ? Right<string, ProcessId>(ProcessId.None)
                : ProcessId.TryParse(pidStr)
                           .Map(FixupRoot)
                           .MapLeft(ex => ex.Message);

        static ProcessName rootName = new ProcessName("root");

        static ProcessId FixupRoot(ProcessId pid) =>
            pid.HeadName() == rootName
                ? Process.Root().Append(pid.Skip(1))
                : pid;
    }
}
