using System;
using System.Linq;
using System.Reactive.Linq;
using Echo.Traits;
using static LanguageExt.Prelude;
using static LanguageExt.Map;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Echo
{
    /// <summary>
    /// <para>
    ///     Process:  Forward functions
    /// </para>
    /// <para>
    ///     'fwd' is used to forward a message onto another process whilst maintaining the original 
    ///     sender context (for 'ask' responses to go back to the right place).
    /// </para>
    /// </summary>
    public static partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        /// <summary>
        /// Forward a message
        /// </summary>
        /// <param name="pid">Process ID to send to</param>
        /// <param name="message">Message to send</param>
        public static Aff<RT, Unit> fwd<T>(ProcessId pid, T message) =>
            Eff(() => Process.fwd(pid, message));

        /// <summary>
        /// Forward a message
        /// </summary>
        /// <param name="pid">Process ID to send to</param>
        public static Aff<RT, Unit> fwd(ProcessId pid) =>
            Eff(() => Process.fwd(pid));

        /// <summary>
        /// Forward a message to a named child process
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="name">Name of the child process</param>
        public static Aff<RT, Unit> fwdChild<T>(ProcessName name, T message) =>
            Eff(() => Process.fwdChild(name, message));

        /// <summary>
        /// Forward a message to a child process (found by index)
        /// </summary>
        /// <remarks>
        /// Because of the potential changeable nature of child nodes, this will
        /// take the index and mod it by the number of children.  We expect this 
        /// call will mostly be used for load balancing, and round-robin type 
        /// behaviour, so feel that's acceptable.  
        /// </remarks>
        /// <param name="message">Message to send</param>
        /// <param name="index">Index of the child process (see remarks)</param>
        public static Aff<RT, Unit> fwdChild<T>(int index, T message) =>
            Eff(() => Process.fwdChild(index, message));

        /// <summary>
        /// Forward a message to a named child process
        /// </summary>
        /// <param name="name">Name of the child process</param>
        public static Aff<RT, Unit> fwdChild(ProcessName name) =>
            Eff(() => Process.fwdChild(name));

        /// <summary>
        /// Forward a message to a child process (found by index)
        /// </summary>
        /// <remarks>
        /// Because of the potential changeable nature of child nodes, this will
        /// take the index and mod it by the number of children.  We expect this 
        /// call will mostly be used for load balancing, and round-robin type 
        /// behaviour, so feel that's acceptable.  
        /// </remarks>
        /// <param name="index">Index of the child process (see remarks)</param>
        public static Aff<RT, Unit> fwdChild(int index) =>
            Eff(() => Process.fwdChild(index));
    }
}
