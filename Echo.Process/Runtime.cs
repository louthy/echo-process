using System;
using System.Text;
using System.Threading;
using Echo.Traits;
using LanguageExt;
using LanguageExt.Effects.Traits;
using LanguageExt.Sys.Traits;
using static LanguageExt.Prelude;
using Sys = LanguageExt.Sys;

namespace Echo
{
    /// <summary>
    /// Example runtime with the core basic IO of Echo and the System namespace
    /// </summary>
    public readonly struct Runtime : 
        HasCancel<Runtime>,
        HasConsole<Runtime>,
        HasFile<Runtime>,
        HasEncoding<Runtime>,
        HasTextRead<Runtime>,
        HasTime<Runtime>,
        HasEnvironment<Runtime>,
        HasDirectory<Runtime>,
        HasEcho<Runtime>
    {
        readonly RuntimeEnv env;

        /// <summary>
        /// Constructor
        /// </summary>
        Runtime(RuntimeEnv env) =>
            this.env = env;

        /// <summary>
        /// Configuration environment accessor
        /// </summary>
        public RuntimeEnv Env =>
            env ?? throw new InvalidOperationException("Runtime Env not set.  Perhaps because of using default(Runtime) or new Runtime() rather than Runtime.New()");
        
        /// <summary>
        /// Constructor function
        /// </summary>
        public static Runtime New(EchoState<Runtime> state) =>
            new Runtime(new RuntimeEnv(new CancellationTokenSource(), System.Text.Encoding.Default, state));

        /// <summary>
        /// Constructor function
        /// </summary>
        /// <param name="state">Echo state</param>
        /// <param name="source">Cancellation token source</param>
        public static Runtime New(EchoState<Runtime> state, CancellationTokenSource source) =>
            new Runtime(new RuntimeEnv(source, System.Text.Encoding.Default, state));

        /// <summary>
        /// Constructor function
        /// </summary>
        /// <param name="state">Echo state</param>
        /// <param name="encoding">Text encoding</param>
        public static Runtime New(EchoState<Runtime> state, Encoding encoding) =>
            new Runtime(new RuntimeEnv(new CancellationTokenSource(), encoding, state));

        /// <summary>
        /// Constructor function
        /// </summary>
        /// <param name="state">Echo state</param>
        /// <param name="encoding">Text encoding</param>
        /// <param name="source">Cancellation token source</param>
        public static Runtime New(EchoState<Runtime> state, Encoding encoding, CancellationTokenSource source) =>
            new Runtime(new RuntimeEnv(source, encoding, state));

        /// <summary>
        /// Create a new Runtime with a fresh cancellation token
        /// </summary>
        /// <remarks>Used by localCancel to create new cancellation context for its sub-environment</remarks>
        /// <returns>New runtime</returns>
        public Runtime LocalCancel =>
            new Runtime( new RuntimeEnv(new CancellationTokenSource(), Env.Encoding, Env.EchoState));

        /// <summary>
        /// Direct access to cancellation token
        /// </summary>
        public CancellationToken CancellationToken =>
            Env.Token;

        /// <summary>
        /// Directly access the cancellation token source
        /// </summary>
        /// <returns>CancellationTokenSource</returns>
        public CancellationTokenSource CancellationTokenSource =>
            Env.Source;

        /// <summary>
        /// Get encoding
        /// </summary>
        /// <returns></returns>
        public Encoding Encoding =>
            Env.Encoding;

        /// <summary>
        /// Access the console environment
        /// </summary>
        /// <returns>Console environment</returns>
        public Eff<Runtime, LanguageExt.Sys.Traits.ConsoleIO> ConsoleEff =>
            SuccessEff(Sys.Live.ConsoleIO.Default);

        /// <summary>
        /// Access the file environment
        /// </summary>
        /// <returns>File environment</returns>
        public Eff<Runtime, Sys.Traits.FileIO> FileEff =>
            SuccessEff(Sys.Live.FileIO.Default);

        /// <summary>
        /// Access the directory environment
        /// </summary>
        /// <returns>Directory environment</returns>
        public Eff<Runtime, Sys.Traits.DirectoryIO> DirectoryEff =>
            SuccessEff(Sys.Live.DirectoryIO.Default);

        /// <summary>
        /// Access the TextReader environment
        /// </summary>
        /// <returns>TextReader environment</returns>
        public Eff<Runtime, Sys.Traits.TextReadIO> TextReadEff =>
            SuccessEff(Sys.Live.TextReadIO.Default);

        /// <summary>
        /// Access the time environment
        /// </summary>
        /// <returns>Time environment</returns>
        public Eff<Runtime, Sys.Traits.TimeIO> TimeEff  =>
            SuccessEff(Sys.Live.TimeIO.Default);

        /// <summary>
        /// Access the operating-system environment
        /// </summary>
        /// <returns>Operating-system environment environment</returns>
        public Eff<Runtime, Sys.Traits.EnvironmentIO> EnvironmentEff =>
            SuccessEff(Sys.Live.EnvironmentIO.Default);

        /// <summary>
        /// Echo state
        /// </summary>
        public EchoState<Runtime> EchoState =>
            Env.EchoState;

        /// <summary>
        /// Map the echo state
        /// </summary>
        public Runtime MapEchoState(Func<EchoState<Runtime>, EchoState<Runtime>> f) =>
            new Runtime(Env.MapEchoState(f));

        /// <summary>
        /// Echo IO
        /// </summary>
        public Eff<Runtime, EchoIO> EchoEff =>
            SuccessEff(LiveEchoIO.Default);
    }
    
    public record RuntimeEnv(CancellationTokenSource Source, CancellationToken Token, Encoding Encoding, EchoState<Runtime> EchoState)
    {
        public RuntimeEnv(CancellationTokenSource source, Encoding encoding, EchoState<Runtime> echoState) : 
            this(source, source.Token, encoding, echoState)
        {
        }

        public RuntimeEnv LocalCancel 
        {
            get
            {
                var source = new CancellationTokenSource();
                return this with {Source = source, Token = source.Token};
            }
        }

        public RuntimeEnv MapEchoState(Func<EchoState<Runtime>, EchoState<Runtime>> f) =>
            this with {EchoState = f(EchoState)};
    }
}