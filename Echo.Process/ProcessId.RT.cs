using Echo.Traits;
using LanguageExt;
using System.Collections.Generic;
using LanguageExt.Effects.Traits;

namespace Echo
{
    /// <summary>
    /// <para>
    /// Process identifier
    /// </para>
    /// <para>
    /// Use this to 'tell' a message to a process.  It can be serialised and passed around
    /// without concerns for internals.
    /// </para>
    /// </summary>
    public static class ProcessIdRuntimeExtensions
    {
        /// <summary>
        /// The Process system qualifier
        /// </summary>
        public static Eff<RT, SystemName> System<RT>(this Eff<RT, ProcessId> pid) 
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.System);

        /// <summary>
        /// Absolute path of the process ID
        /// </summary>
        public static Eff<RT, string> Path<RT>(this Eff<RT, ProcessId> pid) 
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.Path);

        /// <summary>
        /// Generate new ProcessId that represents a child of this process ID
        /// </summary>
        /// <returns>Process ID</returns>
        public static Eff<RT, ProcessId> Child<RT>(this Eff<RT, ProcessId> pid, ProcessName name)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(p => p.Child(name));        

        /// <summary>
        /// Generate new ProcessId that represents a child of this process ID
        /// </summary>
        /// <returns>Process ID</returns>
        public static Eff<RT, ProcessId> Child<RT>(this Eff<RT, ProcessId> pid, IEnumerable<ProcessId> name)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(p => p.Child(name));        

        /// <summary>
        /// Returns true if the ProcessId represents a selection of N process
        /// paths
        /// </summary>
        public static Eff<RT, bool> IsSelection<RT>(this Eff<RT, ProcessId> pid)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.IsSelection);        

        /// <summary>
        /// If this ProcessId represents a selection of N process paths then
        /// this function will return those paths as separate ProcessIds.
        /// </summary>
        /// <returns>An enumerable of ProcessIds representing the selection.
        /// Zero ProcessIds will be returned if the ProcessId is invalid.
        /// One ProcessId will be returned if this ProcessId doesn't represent
        /// a selection.
        /// </returns>
        public static Eff<RT, Seq<ProcessId>> GetSelection<RT>(this Eff<RT, ProcessId> pid)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.GetSelection().ToSeq());        

        /// <summary>
        /// Get the parent ProcessId
        /// </summary>
        /// <returns>Parent process ID</returns>
        public static Eff<RT, ProcessId> Parent<RT>(this Eff<RT, ProcessId> pid)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.Parent);
        
        /// <summary>
        /// Returns true if this is a valid process ID
        /// </summary>
        public static Eff<RT, bool> IsValid<RT>(this Eff<RT, ProcessId> pid)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.IsValid);        

        /// <summary>
        /// Get the name of the process
        /// </summary>
        /// <returns></returns>
        public static Eff<RT, ProcessName> Name<RT>(this Eff<RT, ProcessId> pid)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.Name);
        
        /// <summary>
        /// Remove path elements from the start of the path
        /// </summary>
        public static Eff<RT, ProcessId> Skip<RT>(this Eff<RT, ProcessId> pid, int count)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(p => p.Skip(count));

        /// <summary>
        /// Take N elements of the path
        /// </summary>
        public static Eff<RT, ProcessId> Take<RT>(this Eff<RT, ProcessId> pid, int count)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(p => p.Take(count));

        /// <summary>
        /// Accessor to the head of the path as a ProcessName
        /// </summary>
        /// <returns></returns>
        public static Eff<RT, ProcessName> HeadName<RT>(this Eff<RT, ProcessId> pid)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.HeadName());

        /// <summary>
        /// Take head of path
        /// </summary>
        public static Eff<RT, ProcessId> Head<RT>(this Eff<RT, ProcessId> pid)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.Head());

        /// <summary>
        /// Take tail of path
        /// </summary>
        public static Eff<RT, ProcessId> Tail<RT>(this Eff<RT, ProcessId> pid)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.Tail());

        /// <summary>
        /// Number of parts to the name
        /// </summary>
        /// <returns></returns>
        public static Eff<RT, int> Count<RT>(this Eff<RT, ProcessId> pid)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(static p => p.Count());

        /// <summary>
        /// Append one process ID to another
        /// </summary>
        public static Eff<RT, ProcessId> Append<RT>(this Eff<RT, ProcessId> pid, ProcessId rhs)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(p => p.Append(rhs));

        /// <summary>
        /// Append one process ID to another
        /// </summary>
        public static Eff<RT, ProcessId> Append<RT>(this Eff<RT, ProcessId> pid, Eff<RT, ProcessId> rhs)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Bind(p => rhs.Map(p.Append));

        /// <summary>
        /// Set the system of the process ID
        /// </summary>
        public static Eff<RT, ProcessId> SetSystem<RT>(this Eff<RT, ProcessId> pid, SystemName system)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(p => p.SetSystem(system));

        /// <summary>
        /// True if the process ID starts with head
        /// </summary>
        public static Eff<RT, bool> StartsWith<RT>(this Eff<RT, ProcessId> pid, ProcessId head)
            where RT : struct, HasCancel<RT>, HasEcho<RT> =>
            pid.Map(p => p.StartsWith(head));
    }
}
