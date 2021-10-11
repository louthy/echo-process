using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Echo.Traits;
using LanguageExt.Effects.Traits;
using static Echo.Process;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class Router<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        /// <summary>
        /// Spawn a router using the settings in the config
        /// </summary>
        /// <example>
        /// <para>
        ///     router broadcast1: 
        ///         pid:			/root/user/broadcast1
        ///         route:	        broadcast
        ///         worker-count:	10
        /// </para>
        /// <para>
        ///     router broadcast2: 
        ///         pid:			/root/user/broadcast2
        ///         route:	        broadcast
        ///         workers:		[hello, world]
        /// </para>
        /// <para>
        ///     router least: 
        ///         pid:			/role/user/least
        ///         route:	        least-busy
        ///         workers:		[one, two, three]
        /// </para>
        /// </example>
        /// <param name="name">Name of the child process that will be the router</param>
        /// <returns>ProcessId of the router</returns>
        public static Aff<RT, ProcessId> fromConfig<T>(ProcessName name) =>
            Eff(() => Router.fromConfig<T>(name));

        /// <summary>
        /// Spawn a router using the settings in the config
        /// </summary>
        /// <example>
        /// <para>
        ///     router broadcast1: 
        ///         pid:			/root/user/broadcast1
        ///         route:	        broadcast
        ///         worker-count:	10
        /// </para>
        /// <para>
        ///     router broadcast2: 
        ///         pid:			/root/user/broadcast2
        ///         route:	        broadcast
        ///         workers:		[hello, world]
        /// </para>
        /// <para>
        ///     router least: 
        ///         pid:			/role/user/least
        ///         route:	        least-busy
        ///         workers:		[one, two, three]
        /// </para>
        /// </example>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">Name of the child process that will be the router</param>
        /// <returns>ProcessId of the router</returns>
        public static Aff<RT, ProcessId> fromConfig<T>(ProcessName name, Func<T, Unit> Inbox) =>
            Eff(() => Router.fromConfig<T>(name, Inbox));

        /// <summary>
        /// Spawn a router using the settings in the config
        /// </summary>
        /// <example>
        /// <para>
        ///     router broadcast1: 
        ///         pid:			/root/user/broadcast1
        ///         route:	        broadcast
        ///         worker-count:	10
        /// </para>
        /// <para>
        ///     router broadcast2: 
        ///         pid:			/root/user/broadcast2
        ///         route:	        broadcast
        ///         workers:		[hello, world]
        /// </para>
        /// <para>
        ///     router least: 
        ///         pid:			/role/user/least
        ///         route:	        least-busy
        ///         workers:		[one, two, three]
        /// </para>
        /// </example>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">Name of the child process that will be the router</param>
        /// <returns>ProcessId of the router</returns>
        public static Aff<RT, ProcessId> fromConfig<T>(ProcessName name, Action<T> Inbox) =>
            Eff(() => Router.fromConfig<T>(name, Inbox));

        /// <summary>
        /// Spawn a router using the settings in the config
        /// </summary>
        /// <example>
        /// <para>
        ///     router broadcast1: 
        ///         pid:			/root/user/broadcast1
        ///         route:	        broadcast
        ///         worker-count:	10
        /// </para>
        /// <para>
        ///     router broadcast2: 
        ///         pid:			/root/user/broadcast2
        ///         route:	        broadcast
        ///         workers:		[hello, world]
        /// </para>
        /// <para>
        ///     router least: 
        ///         pid:			/role/user/least
        ///         route:	        least-busy
        ///         workers:		[one, two, three]
        /// </para>
        /// </example>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">Name of the child process that will be the router</param>
        /// <returns>ProcessId of the router</returns>
        public static Aff<RT, ProcessId> fromConfig<S, T>(ProcessName name, Func<S> Setup, Func<S,T,S> Inbox) =>
            Eff(() => Router.fromConfig<S, T>(name, Setup, Inbox));
    }
}
