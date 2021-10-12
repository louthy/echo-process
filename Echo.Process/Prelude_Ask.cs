using LanguageExt;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LanguageExt.Common;
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
    public static partial class Process
    {
        /// <summary>
        /// Ask a process for a reply
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Sender process</param>
        /// <returns>The response to the request</returns>
        public static T ask<T>(ProcessId pid, object message, ProcessId sender) =>
            ActorContext.System(pid).Ask<T>(pid, message, sender);

        /// <summary>
        /// Ask a process for a reply
        /// </summary>
        /// <remarks>Never throws, instead returns a Fin representing success or failure</remarks>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Sender process</param>
        /// <returns>The response to the request</returns>
        public static Fin<T> askSafe<T>(ProcessId pid, object message, ProcessId sender)
        {
            try
            {
                return ActorContext.System(pid).Ask<T>(pid, message, sender);
            }
            catch (Exception e)
            {
                dead(message, e);
                return (Error) e;
            }
        }

        /// <summary>
        /// Ask a process for a reply
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <returns>The response to the request</returns>
        public static T ask<T>(ProcessId pid, object message) =>
            ask<T>(pid, message, Self);

        /// <summary>
        /// Ask a process for a reply
        /// </summary>
        /// <remarks>Never throws, instead returns a Fin representing success or failure</remarks>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <returns>The response to the request</returns>
        public static Fin<T> askSafe<T>(ProcessId pid, object message) =>
            askSafe<T>(pid, message, Self);

        /// <summary>
        /// Asynchronous ask - must be used outside of a Process
        /// </summary>
        /// <typeparam name="R">Type of the return value</typeparam>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Sender process</param>
        /// <returns>A promise to return a response to the request</returns>
        public static async Task<R> askAsync<R>(ProcessId pid, object message, ProcessId sender) =>
            InMessageLoop
                ? raiseDontUseInMessageLoopException<R>(nameof(observeState))
                : await Task.Run(() => ask<R>(pid, message, sender)).ConfigureAwait(false);

        /// <summary>
        /// Asynchronous ask - must be used outside of a Process
        /// </summary>
        /// <remarks>Never throws, instead returns a Fin representing success or failure</remarks>
        /// <typeparam name="R">Type of the return value</typeparam>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Sender process</param>
        /// <returns>A promise to return a response to the request</returns>
        public static async Task<Fin<R>> askAsyncSafe<R>(ProcessId pid, object message, ProcessId sender) =>
            InMessageLoop
                ? raiseDontUseInMessageLoopException<R>(nameof(observeState))
                : await Task.Run(() => askSafe<R>(pid, message, sender)).ConfigureAwait(false);

        /// <summary>
        /// Ask a process for a reply (if the process is running).  If the process isn't running
        /// then None is returned
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Sender process</param>
        /// <returns>The response to the request or None if the process isn't running</returns>
        public static Option<T> askIfAlive<T>(ProcessId pid, object message, ProcessId sender) =>
            ping(pid)
                ? askSafe<T>(pid, message, sender).ToOption()
                : None;

        /// <summary>
        /// Ask a process for a reply (if the process is running).  If the process isn't running
        /// then None is returned
        /// </summary>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <returns>The response to the request or None if the process isn't running</returns>
        public static Option<T> askIfAlive<T>(ProcessId pid, object message) =>
            ping(pid)
                ? askSafe<T>(pid, message, Self).ToOption()
                : None;

        /// <summary>
        /// Asynchronous ask - must be used outside of a Process
        /// </summary>
        /// <remarks>Never throws, instead returns a Fin representing success or failure</remarks>
        /// <typeparam name="R">Type of the return value</typeparam>
        /// <param name="pid">Process to ask</param>
        /// <param name="message">Message to send</param>
        /// <returns>A promise to return a response to the request</returns>
        public static Task<Fin<R>> askAsyncSafe<R>(ProcessId pid, object message) =>
            askAsyncSafe<R>(pid, message, Self);

        /// <summary>
        /// Ask children the same message
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns></returns>
        public static Seq<T> askChildren<T>(object message, int take = Int32.MaxValue)
        {
            try
            {
                return ActorContext.System(default(SystemName)).AskMany<T>(Children.Values.ToSeq(), message, take).ToSeq();
            }
            catch (Exception e)
            {
                dead(message, e, "askChildren: child has probably died since calling Children");
                return Empty;
            }
        }

        /// <summary>
        /// Ask children the same message
        /// </summary>
        /// <remarks>Never throws, instead returns a Fin representing success or failure</remarks>
        /// <param name="message">Message to send</param>
        /// <returns></returns>
        public static Fin<Seq<T>> askChildrenSafe<T>(object message, int take = Int32.MaxValue)
        {
            try
            {
                return ActorContext.System(default(SystemName)).AskMany<T>(Children.Values.ToSeq(), message, take).ToSeq();
            }
            catch (Exception e)
            {
                dead(message, e, "askChildren: child has probably died since calling Children");
                return (Error)e;
            }
        }        

        /// <summary>
        /// Ask parent process for a reply
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>The response to the request</returns>
        public static T askParent<T>(object message) =>
            ask<T>(Parent, message);

        /// <summary>
        /// Ask parent process for a reply
        /// </summary>
        /// <remarks>Never throws, instead returns a Fin representing success or failure</remarks>
        /// <param name="message">Message to send</param>
        /// <returns>The response to the request</returns>
        public static Fin<T> askParentSafe<T>(object message) =>
            askSafe<T>(Parent, message);

        /// <summary>
        /// Ask a named child process for a reply
        /// </summary>
        /// <remarks>Never throws, instead returns a Fin representing success or failure</remarks>
        /// <param name="message">Message to send</param>
        /// <param name="name">Name of the child process</param>
        public static Fin<T> askChildSafe<T>(ProcessName name, object message)
        {
            try
            {
                return ask<T>(Self.Child(name), message);
            }
            catch (Exception e)
            {
                dead(message, e, "askChild: child has probably died since calling Children");
                return (Error)e;
            }
        }

        /// <summary>
        /// Ask a child process (found by index) for a reply
        /// </summary>
        /// <remarks>Never throws, instead returns a Fin representing success or failure</remarks>
        /// <remarks>
        /// Because of the potential changeable nature of child nodes, this will
        /// take the index and mod it by the number of children.  We expect this 
        /// call will mostly be used for load balancing, and round-robin type 
        /// behaviour, so feel that's acceptable.  
        /// </remarks>
        /// <param name="message">Message to send</param>
        /// <param name="index">Index of the child process (see remarks)</param>
        public static Fin<T> askChildSafe<T>(int index, object message)
        {
            try
            {
                return ask<T>(child(index), message);
            }
            catch (Exception e)
            {
                dead(message, e, "askChild: child has probably died since calling Children");
                return (Error)e;
            }
        }        

        /// <summary>
        /// Ask a named child process for a reply
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="name">Name of the child process</param>
        public static T askChild<T>(ProcessName name, object message) =>
            ask<T>(Self.Child(name), message);

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
        public static T askChild<T>(int index, object message) =>
            ask<T>(child(index), message);
    }
}
