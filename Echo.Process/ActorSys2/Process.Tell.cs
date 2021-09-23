using System;
using Echo.Traits;
using LanguageExt;
using Echo.ActorSys2;
using LanguageExt.Sys.Traits;
using LanguageExt.ClassInstances;
using System.Collections.Generic;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo
{
    public static partial class Process<RT>  
        where RT : struct, HasEcho<RT>, HasTime<RT>
    {
        /// <summary>
        /// Tell a message to a process
        /// </summary>
        /// <param name="pid">Process to tell</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Optional sender</param>
        /// <typeparam name="A">Type of the message</typeparam>
        /// <returns>Unit if succeeds</returns>
        public static Aff<RT, Unit> tell<A>(ProcessId pid, A message, ProcessId sender = default(ProcessId)) =>
            from s in sender.IsValid ? SuccessEff(sender) : Self | User
            from _ in post(pid, new UserPost(s, message, 0))
            select unit;

        /// <summary>
        /// Tell a message to the parent process
        /// </summary>
        /// <remarks>
        /// If this is called outside of a process inbox then the message goes to dead-letters 
        /// </remarks>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Optional sender</param>
        /// <typeparam name="A">Type of the message</typeparam>
        /// <returns>Unit if succeeds</returns>
        public static Aff<RT, Unit> tellParent<A>(A message, ProcessId sender = default(ProcessId)) =>
            getParent.Bind(p => tell(p, message, sender));

        /// <summary>
        /// Tell a message to a child of the current process
        /// </summary>
        /// <param name="child">Name of the child process</param>
        /// <param name="message">Message to send</param>
        /// <param name="sender">Optional sender</param>
        /// <typeparam name="A">Type of the message</typeparam>
        /// <returns>Unit if succeeds</returns>
        public static Aff<RT, Unit> tellChild<A>(ProcessName child, A message, ProcessId sender = default(ProcessId)) =>
            getSelf.Bind(s => tell(s.Child(child), message, sender));
        
        /// <summary>
        /// Tell a system message to a process
        /// </summary>
        /// <param name="pid">Process to tell</param>
        /// <param name="message">Message to send</param>
        /// <returns>Unit if succeeds</returns>
        internal static Aff<RT, Unit> tellSystem(ProcessId pid, SysPost message) =>
            post(pid, message);
        
        /// <summary>
        /// Tell a system message to a process
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>Unit if succeeds</returns>
        internal static Aff<RT, Unit> tellParentSystem(SysPost message) =>
            getParent.Bind(pid => post(pid, message));
        
        /// <summary>
        /// Tell a system message to a process
        /// </summary>
        /// <param name="child">Name of the child process</param>
        /// <param name="message">Message to send</param>
        /// <returns>Unit if succeeds</returns>
        internal static Aff<RT, Unit> tellChildSystem(ProcessName child, SysPost message) =>
            getSelf.Bind(p => post(p.Child(child), message));
    }
}