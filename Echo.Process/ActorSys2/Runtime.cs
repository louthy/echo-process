using System;
using Echo.Traits;
using LanguageExt;
using System.Threading;
using LanguageExt.Sys.Traits;
using System.Collections.Generic;
using static LanguageExt.Prelude;

namespace Echo.ActorSys2
{
    public readonly struct Runtime : 
        HasEcho<Runtime>,
        HasTime<Runtime>
    {
        readonly RuntimeEnv Env;
        
        Runtime(RuntimeEnv env) =>
            Env = env;

        public static Runtime New() =>
            new Runtime(new RuntimeEnv(EchoState<Runtime>.Default, new CancellationTokenSource()));

        public Runtime LocalCancel =>
            new Runtime(new RuntimeEnv(EchoState<Runtime>.Default));

        public CancellationToken CancellationToken =>
            Env.Token;

        public CancellationTokenSource CancellationTokenSource =>
            Env.Source;

        public EchoState<Runtime> EchoState =>
            Env.EchoState;

        public Runtime LocalEcho(Func<EchoState<Runtime>, EchoState<Runtime>> f) =>
            new Runtime(Env with {EchoState = f(Env.EchoState)});

        public Eff<Runtime, EchoIO<Runtime>> EchoEff =>
            EchoLive.Default;

        public Eff<Runtime, TimeIO> TimeEff =>
            SuccessEff(LanguageExt.Sys.Live.TimeIO.Default);
    }

    public record RuntimeEnv(EchoState<Runtime> EchoState, CancellationTokenSource Source, CancellationToken Token)
    {
        public RuntimeEnv(EchoState<Runtime> EchoState, CancellationTokenSource Source) : this(EchoState, Source, Source.Token)
        {
        }

        public RuntimeEnv(EchoState<Runtime> EchoState) : this(EchoState, new CancellationTokenSource())
        {
        }
    }

    public class EchoLive : EchoIO<Runtime>
    {
        public static readonly Eff<Runtime, EchoIO<Runtime>> Default = SuccessEff<EchoIO<Runtime>>(new EchoLive());
        
        public Aff<Runtime, Unit> Watch(ProcessId watcher, ProcessId watched) =>
            throw new NotImplementedException();

        public Aff<Runtime, Unit> UnWatch(ProcessId watcher, ProcessId watched) =>
            throw new NotImplementedException();

        public Aff<Runtime, Unit> ForwardToDeadLetters(Post post) =>
            throw new NotImplementedException();

        public Aff<Runtime, Unit> Tell(ProcessId pid, Post msg) =>
            throw new NotImplementedException();

        public Aff<Runtime, Unit> TellMany(IEnumerable<ProcessId> pids, Post msg) =>
            throw new NotImplementedException();

        public Eff<Runtime, Unit> Log(ProcessLogItem item) =>
            throw new NotImplementedException();
    }
}