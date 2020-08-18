using System;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using LanguageExt;
using static LanguageExt.Prelude;
using LanguageExt.Interfaces;
using StackExchange.Redis;

namespace Echo
{
    /// <summary>
    /// Implementation of HasEcho, which has all the traits needed to do the IO for Echo
    /// </summary>
    public struct RedisRuntime : HasEcho<RedisRuntime>
    {
        readonly CancellationTokenSource source;

        /// <summary>
        /// Create a new runtime 
        /// </summary>
        public static RedisRuntime New() =>
            new RedisRuntime(new CancellationTokenSource(), EchoEnv.Default);
        
        /// <summary>
        /// Private ctor
        /// </summary>
        RedisRuntime(CancellationTokenSource source, EchoEnv echoEnv) =>
            (this.source, EchoEnv, CancellationToken) =
                (source, echoEnv, source?.Token ?? default);

        /// <summary>
        /// Maps a RedisRuntime to have a new cancellation context
        /// </summary>
        /// <returns>Runtime with a localised cancellation context</returns>
        public RedisRuntime LocalCancel =>
            new RedisRuntime(
                new CancellationTokenSource(),
                EchoEnv);
 
        /// <summary>
        /// Access to the echo environment
        /// </summary>
        public EchoEnv EchoEnv { get; }

        /// <summary>
        /// Set the SystemName
        /// </summary>
        public RedisRuntime SetEchoEnv(EchoEnv echoEnv) =>
            new RedisRuntime(
                source,
                echoEnv);

        /// <summary>
        /// Use a local environment
        /// </summary>
        /// <remarks>This is used as the echo system steps into the scope of various processes to set the context
        /// for those processes</remarks>
        public RedisRuntime LocalEchoEnv(EchoEnv echoEnv) =>
            new RedisRuntime(
                source,
                echoEnv);
        
        /// <summary>
        /// Get the echo IO
        /// </summary>
        public Aff<RedisRuntime, EchoIO> EchoAff =>
            EchoEff.ToAsync();

        /// <summary>
        /// Get the echo IO
        /// </summary>
        public Eff<RedisRuntime, EchoIO> EchoEff =>
            Eff<RedisRuntime, EchoIO>(env => new RedisEchoIO(Cluster.configOrThrow(env.SystemName), DefaultSerialiseIO.Default));
        
        /// <summary>
        /// Get the SerialiseIO
        /// </summary>
        public Aff<RedisRuntime, SerialiseIO> SerialiseAff =>
            SuccessEff(DefaultSerialiseIO.Default);

        /// <summary>
        /// Get the SerialiseIO
        /// </summary>
        public Eff<RedisRuntime, SerialiseIO> SerialiseEff =>
            SuccessEff(DefaultSerialiseIO.Default);

        /// <summary>
        /// Access the time IO environment
        /// </summary>
        /// <returns>Time IO environment</returns>
        public Aff<RedisRuntime, TimeIO> TimeAff =>
            SuccessAff(LanguageExt.LiveIO.TimeIO.Default);

        /// <summary>
        /// Access the time SIO environment
        /// </summary>
        /// <returns>Time SIO environment</returns>
        public Eff<RedisRuntime, TimeIO> TimeEff  =>
            SuccessEff(LanguageExt.LiveIO.TimeIO.Default);
        
        /// <summary>
        /// Access the file IO environment
        /// </summary>
        /// <returns>File IO environment</returns>
        public Aff<RedisRuntime, FileIO> FileAff =>
            SuccessAff(LanguageExt.LiveIO.FileIO.Default);
        
        /// <summary>
        /// Access the file SIO environment
        /// </summary>
        /// <returns>File SIO environment</returns>
        public Eff<RedisRuntime, FileIO> FileEff =>
            SuccessEff(LanguageExt.LiveIO.FileIO.Default);

        /// <summary>
        /// Direct access to cancellation token
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Directly access the cancellation token source
        /// </summary>
        /// <returns>CancellationTokenSource</returns>
        public Eff<RedisRuntime, CancellationTokenSource> CancellationTokenSource =>
            Eff<RedisRuntime, CancellationTokenSource>(env => env.source);
        
        /// <summary>
        /// Get the cancellation token
        /// </summary>
        /// <returns>CancellationToken</returns>
        public Eff<RedisRuntime, CancellationToken> Token =>
            Eff<RedisRuntime, CancellationToken>(env => env.CancellationToken);
 
        /// <summary>
        /// Get encoding
        /// </summary>
        /// <returns></returns>
        public Eff<RedisRuntime, Encoding> Encoding =>
            Eff<RedisRuntime, Encoding>(_ => System.Text.Encoding.Default);
    }
}