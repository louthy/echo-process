using System;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo;

/// <summary>
/// <para>
///     Process:  Ask functions
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
    where RT : struct, HasEcho<RT>
{
    /// <summary>
    /// Send a message to a process
    /// </summary>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Aff<RT, A> ask<A>(ProcessId pid, object message, ProcessId sender = default(ProcessId)) =>
        Eff<RT, A>(_ => Process.ask<A>(pid, message, sender));

    /// <summary>
    /// Send a message to a process
    /// </summary>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Aff<RT, A> ask<A>(Eff<RT, ProcessId> pid, object message, ProcessId sender = default(ProcessId)) =>
        pid.Map(id => Process.ask<A>(id, message, sender));

    /// <summary>
    /// Ask children the same message
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="take">Maximum number of children to ask</param>
    public static Aff<RT, Seq<A>> askChildren<A>(object message, int take = Int32.MaxValue) =>
        Eff<RT, Seq<A>>(_ => Process.askChildren<A>(message, take).ToSeq());

    /// <summary>
    /// Send a message to the parent process
    /// </summary>
    /// <param name="message">Message to send</param>
    public static Aff<RT, A> askParent<A>(object message) =>
        Eff<RT, A>(_ => Process.askParent<A>(message));

    /// <summary>
    /// Send a message to a named child process
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="name">Name of the child process</param>
    public static Aff<RT, A> askChild<A>(ProcessName name, A message) =>
        Eff<RT, A>(_ => Process.askChild<A>(name, message));

    /// <summary>
    /// Send a message to a child process (found by index)
    /// </summary>
    /// <remarks>
    /// Because of the potential changeable nature of child nodes, this will
    /// take the index and mod it by the number of children.  We expect this 
    /// call will mostly be used for load balancing, and round-robin type 
    /// behaviour, so feel that's acceptable.  
    /// </remarks>
    /// <param name="message">Message to send</param>
    /// <param name="index">Index of the child process (see remarks)</param>
    public static Aff<RT, A> askChild<A>(int index, object message) =>
        Eff<RT, A>(_ => Process.askChild<A>(index, message));
}
