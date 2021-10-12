using LanguageExt;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Echo.Traits;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;
using static Echo.Process;

namespace Echo
{
    /// <summary>
    /// <para>
    ///     Process: Ask functions
    /// </para>
    /// <para>
    ///     'ask' is a request/response system for processes.  You can ask a process a question (a message) and it
    ///     can reply using the 'Process.reply' function.  It doesn't have to and 'ask' will timeout after 
    ///     ActorConfig.Default.Timeout seconds. 
    /// </para>
    /// <para>
    ///     'ask' is blocking, because mostly it will be called from within a process and processes shouldn't 
    ///     perform asynchronous operations.
    /// </para>
    /// </summary>
    public static partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT> 
    {
        /// <summary>
        /// Ask a process for a reply
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Sender process</param>
        /// <returns>The response to the request</returns>
        public static Aff<RT, T> ask<T>(ProcessId pid, object message, ProcessId sender) =>
            EffMaybe(() => Process.askSafe<T>(pid, message, sender));

        /// <summary>
        /// Ask a process for a reply
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Sender process</param>
        /// <returns>The response to the request</returns>
        public static Aff<RT, T> ask<T>(Eff<RT, ProcessId> pid, object message, ProcessId sender) =>
            pid.Bind(p => askSafe<T>(p, message, sender).ToAff());

        /// <summary>
        /// Ask a process for a reply
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <returns>The response to the request</returns>
        public static Aff<RT, T> ask<T>(ProcessId pid, object message) =>
            EffMaybe(() => Process.askSafe<T>(pid, message));

        /// <summary>
        /// Ask a process for a reply
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <returns>The response to the request</returns>
        public static Aff<RT, T> ask<T>(Eff<RT, ProcessId> pid, object message) =>
            pid.Bind(p => askSafe<T>(p, message).ToAff());

        /// <summary>
        /// Ask a process for a reply (if the process is running).  If the process isn't running
        /// then None is returned
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Sender process</param>
        /// <returns>The response to the request or None if the process isn't running</returns>
        public static Aff<RT, Option<T>> askIfAlive<T>(ProcessId pid, object message, ProcessId sender) =>
            Eff(() => Process.askIfAlive<T>(pid, message, sender));

        /// <summary>
        /// Ask a process for a reply (if the process is running).  If the process isn't running
        /// then None is returned
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Sender process</param>
        /// <returns>The response to the request or None if the process isn't running</returns>
        public static Aff<RT, Option<T>> askIfAlive<T>(Eff<RT, ProcessId> pid, object message, ProcessId sender) =>
            pid.Bind(p => askIfAlive<T>(p, message, sender));

        /// <summary>
        /// Ask a process for a reply (if the process is running).  If the process isn't running
        /// then None is returned
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <returns>The response to the request or None if the process isn't running</returns>
        public static Aff<RT, Option<T>> askIfAlive<T>(ProcessId pid, object message) =>
            Eff(() => Process.askIfAlive<T>(pid, message));

        /// <summary>
        /// Ask a process for a reply (if the process is running).  If the process isn't running
        /// then None is returned
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <returns>The response to the request or None if the process isn't running</returns>
        public static Aff<RT, Option<T>> askIfAlive<T>(Eff<RT, ProcessId> pid, object message) =>
            pid.Bind(p => askIfAlive<T>(p, message));

        /// <summary>
        /// Ask children the same message
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns></returns>
        public static Aff<RT, Seq<T>> askChildren<T>(object message, int take = Int32.MaxValue) =>
            EffMaybe(() => Process.askChildrenSafe<T>(message, take));

        /// <summary>
        /// Ask parent process for a reply
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>The response to the request</returns>
        public static Aff<RT, T> askParent<T>(object message) =>
            EffMaybe(() => Process.askParentSafe<T>(message));

        /// <summary>
        /// Ask a named child process for a reply
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="name">Name of the child process</param>
        public static Aff<RT, T> askChild<T>(ProcessName name, object message) =>
            EffMaybe(() => Process.askChildSafe<T>(name, message));

        /// <summary>
        /// Ask a child process (found by index) for a reply
        /// </summary>
        /// <remarks>
        /// Because of the potential changeable nature of child nodes, this will
        /// take the index and mod it by the number of children.  We expect this 
        /// call will mostly be used for load balancing, and round-robin type 
        /// behaviour, so feel that's acceptable.  
        /// </remarks>
        /// <param name="message">Message to send</param>
        /// <param name="index">Index of the child process (see remarks)</param>
        public static Aff<RT, T> askChild<T>(int index, object message) =>
            EffMaybe(() => Process.askChildSafe<T>(index, message));
    }
}
