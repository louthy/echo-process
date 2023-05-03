using System;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo;

/// <summary>
/// <para>
///     Process:  Tell functions
/// </para>
/// <para>
///     'Tell' is used to send a message from one process to another (or from outside a process to a process).
///     The messages are sent to the process asynchronously and join the process' inbox.  The process will 
///     deal with one message from its inbox at a time.  It cannot start the next message until it's finished
///     with a previous message.
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
    public static Eff<RT, Unit> tell<A>(ProcessId pid, A message, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tell(pid, message, sender));

    /// <summary>
    /// Send a message to a process
    /// </summary>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tell<A>(Eff<RT, ProcessId> pid, A message, ProcessId sender = default(ProcessId)) =>
        pid.Map(id => Process.tell(id, message, sender));
    
    /// <summary>
    /// Send a message to a process
    /// </summary>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    internal static Eff<RT, Unit> tellSystem<A>(ProcessId pid, A message) =>
        Eff<RT, Unit>(_ => Process.tellSystem(pid, message));
    
    /// <summary>
    /// Send a message to a process
    /// </summary>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    internal static Eff<RT, Unit> tellSystem<A>(Eff<RT, ProcessId> pid, A message) =>
        pid.Map(id => Process.tellSystem(id, message));

    /// <summary>
    /// Send a message at a specified time in the future
    /// </summary>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    /// <param name="schedule">A structure that defines the method of delivery of the scheduled message</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tell<A>(ProcessId pid, A message, Schedule schedule, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tell(pid, message, schedule, sender));

    /// <summary>
    /// Send a message at a specified time in the future
    /// </summary>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    /// <param name="schedule">A structure that defines the method of delivery of the scheduled message</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tell<A>(Eff<RT, ProcessId> pid, A message, Schedule schedule, ProcessId sender = default(ProcessId)) =>
        pid.Map(id => Process.tell(id, message, schedule, sender));

    /// <summary>
    /// Send a message at a specified time in the future
    /// </summary>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    /// <param name="delayFor">How long to delay sending for</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tell<A>(ProcessId pid, A message, TimeSpan delayFor, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tell(pid, message, delayFor, sender));

    /// <summary>
    /// Send a message at a specified time in the future
    /// </summary>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    /// <param name="delayFor">How long to delay sending for</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tell<A>(Eff<RT, ProcessId> pid, A message, TimeSpan delayFor, ProcessId sender = default(ProcessId)) =>
        pid.Map(id => Process.tell(id, message, delayFor, sender));

    /// <summary>
    /// Send a message at a specified time in the future
    /// </summary>
    /// <remarks>
    /// It is advised to use the variant that takes a TimeSpan, this will fail to be accurate across a Daylight Saving 
    /// Time boundary or if you use non-UTC dates
    /// </remarks>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    /// <param name="delayUntil">Date and time to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tell<A>(ProcessId pid, A message, DateTime delayUntil, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tell(pid, message, delayUntil, sender));

    /// <summary>
    /// Send a message at a specified time in the future
    /// </summary>
    /// <remarks>
    /// It is advised to use the variant that takes a TimeSpan, this will fail to be accurate across a Daylight Saving 
    /// Time boundary or if you use non-UTC dates
    /// </remarks>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="pid">Process ID to send to</param>
    /// <param name="message">Message to send</param>
    /// <param name="delayUntil">Date and time to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tell<A>(Eff<RT, ProcessId> pid, A message, DateTime delayUntil, ProcessId sender = default(ProcessId)) =>
        pid.Map(id => Process.tell(id, message, delayUntil, sender));

    /// <summary>
    /// Tell children the same message
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellChildren<A>(A message, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellChildren(message, sender));

    /// <summary>
    /// Tell children the same message
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    internal static Eff<RT, Unit> tellSystemChildren<A>(A message, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellSystemChildren(message, sender));

    /// <summary>
    /// Tell children the same message, delayed.
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="schedule">A structure that defines the method of delivery of the scheduled message</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    public static Eff<RT, Unit> tellChildren<A>(A message, Schedule schedule, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellChildren(message, schedule, sender));

    /// <summary>
    /// Tell children the same message, delayed.
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="delayFor">How long to delay sending for</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    public static Eff<RT, Unit> tellChildren<A>(A message, TimeSpan delayFor, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellChildren(message, delayFor, sender));

    /// <summary>
    /// Tell children the same message, delayed.
    /// </summary>
    /// <remarks>
    /// This will fail to be accurate across a Daylight Saving Time boundary
    /// </remarks>
    /// <param name="message">Message to send</param>
    /// <param name="delayUntil">Date and time to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    public static Eff<RT, Unit> tellChildren<A>(A message, DateTime delayUntil, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellChildren(message, delayUntil, sender));

    /// <summary>
    /// Tell children the same message
    /// The list of children to send to are filtered by the predicate provided
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="predicate">The list of children to send to are filtered by the predicate provided</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellChildren<A>(A message, Func<ProcessId, bool> predicate, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellChildren(message, predicate, sender));

    /// <summary>
    /// Tell children the same message, delayed.
    /// The list of children to send to are filtered by the predicate provided
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="schedule">A structure that defines the method of delivery of the scheduled message</param>
    /// <param name="predicate">The list of children to send to are filtered by the predicate provided</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    public static Eff<RT, Unit> tellChildren<A>(A message, Schedule schedule, Func<ProcessId, bool> predicate, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellChildren(message, schedule, predicate, sender));

    /// <summary>
    /// Tell children the same message, delayed.
    /// The list of children to send to are filtered by the predicate provided
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="delayFor">How long to delay sending for</param>
    /// <param name="predicate">The list of children to send to are filtered by the predicate provided</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    public static Eff<RT, Unit> tellChildren<A>(A message, TimeSpan delayFor, Func<ProcessId, bool> predicate, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellChildren(message, delayFor, predicate, sender));

    /// <summary>
    /// Tell children the same message, delayed.
    /// The list of children to send to are filtered by the predicate provided
    /// </summary>
    /// <remarks>
    /// This will fail to be accurate across a Daylight Saving Time boundary
    /// </remarks>
    /// <param name="message">Message to send</param>
    /// <param name="delayUntil">Date and time to send</param>
    /// <param name="predicate">The list of children to send to are filtered by the predicate provided</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    public static Eff<RT, Unit> tellChildren<A>(A message, DateTime delayUntil, Func<ProcessId, bool> predicate, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellChildren(message, delayUntil, predicate, sender));

    /// <summary>
    /// Send a message to the parent process
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellParent<A>(A message, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellParent(message, sender));

    /// <summary>
    /// Send a message to the parent process at a specified time in the future
    /// </summary>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="message">Message to send</param>
    /// <param name="schedule">A structure that defines the method of delivery of the scheduled message</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellParent<A>(A message, Schedule schedule, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellParent(message, schedule, sender));

    /// <summary>
    /// Send a message to the parent process at a specified time in the future
    /// </summary>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="message">Message to send</param>
    /// <param name="delayFor">How long to delay sending for</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellParent<A>(A message, TimeSpan delayFor, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellParent(message, delayFor, sender));

    /// <summary>
    /// Send a message to the parent process at a specified time in the future
    /// </summary>
    /// <remarks>
    /// This will fail to be accurate across a Daylight Saving Time boundary
    /// </remarks>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="message">Message to send</param>
    /// <param name="delayUntil">Date and time to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellParent<A>(A message, DateTime delayUntil, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellParent(message, delayUntil, sender));

    /// <summary>
    /// Send a message to ourself
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellSelf<A>(A message, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellSelf(message, sender));

    /// <summary>
    /// Send a message to ourself at a specified time in the future
    /// </summary>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="message">Message to send</param>
    /// <param name="schedule">A structure that defines the method of delivery of the scheduled message</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellSelf<A>(A message, Schedule schedule, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellSelf(message, schedule, sender));

    /// <summary>
    /// Send a message to ourself at a specified time in the future
    /// </summary>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="message">Message to send</param>
    /// <param name="delayFor">How long to delay sending for</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellSelf<A>(A message, TimeSpan delayFor, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellSelf(message, delayFor, sender));

    /// <summary>
    /// Send a message to ourself at a specified time in the future
    /// </summary>
    /// <remarks>
    /// This will fail to be accurate across a Daylight Saving Time boundary
    /// </remarks>
    /// <returns>IDisposable that you can use to cancel the operation if necessary.  You do not need to call Dispose 
    /// for any other reason.</returns>
    /// <param name="message">Message to send</param>
    /// <param name="delayUntil">Date and time to send</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellSelf<A>(A message, DateTime delayUntil, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellSelf(message, delayUntil, sender));

    /// <summary>
    /// Send a message to a named child process
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="name">Name of the child process</param>
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellChild<A>(ProcessName name, A message, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellChild(name, message, sender));

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
    /// <param name="sender">Optional sender override.  The sender is handled automatically if you do not provide one.</param>
    public static Eff<RT, Unit> tellChild<A>(int index, A message, ProcessId sender = default(ProcessId)) =>
        Eff<RT, Unit>(_ => Process.tellChild(index, message, sender));
}
