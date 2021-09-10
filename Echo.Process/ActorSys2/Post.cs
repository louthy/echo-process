using Echo.Traits;
using LanguageExt.Common;

namespace Echo.ActorSys2
{
    /// <summary>
    /// Abstract post message
    /// </summary>
    public abstract record Post(ProcessId Sender);

    /// <summary>
    /// Abstract system-post message
    /// </summary>
    internal abstract record SysPost(ProcessId Sender) : Post(Sender);

    /// <summary>
    /// Posted to the parent when a child process faults in its user-inbox
    /// </summary>
    internal record ChildFaultedSysPost(Error Error, UserPost Post, ProcessId Sender) : SysPost(Sender);

    /// <summary>
    /// Posted to the parent when a child process faults in its user-inbox
    /// </summary>
    internal record LinkChildSysPost<RT>(Actor<RT> Child, ProcessId Sender) : SysPost(Sender)
        where RT : struct, HasEcho<RT>;

    /// <summary>
    /// Instruct the Process to shutdown 
    /// </summary>
    internal record ShutdownProcessSysPost(bool MaintainState, ProcessId Sender) : SysPost(Sender);

    /// <summary>
    /// Instruct the Process to restart 
    /// </summary>
    internal record RestartSysPost(int pauseForMilliseconds, ProcessId Sender) : SysPost(Sender);

    /// <summary>
    /// Instruct the Process to resume 
    /// </summary>
    internal record ResumeSysPost(int pauseForMilliseconds, ProcessId Sender) : SysPost(Sender);

    /// <summary>
    /// Instruct the Process to pause 
    /// </summary>
    internal record PauseSysPost(ProcessId Sender) : SysPost(Sender);

    /// <summary>
    /// Instruct the Process to un-pause 
    /// </summary>
    internal record UnpauseSysPost(ProcessId Sender) : SysPost(Sender);

    /// <summary>
    /// Instruct the Process to watch another process 
    /// </summary>
    internal record WatchSysPost(ProcessId ProcessId, ProcessId Sender) : SysPost(Sender);

    /// <summary>
    /// Instruct the Process to un-watch another processes 
    /// </summary>
    internal record UnWatchSysPost(ProcessId ProcessId, ProcessId Sender) : SysPost(Sender);

    /// <summary>
    /// User post message
    /// </summary>
    internal record UserPost(ProcessId Sender, object Message, long RequestId) : Post(Sender);
}