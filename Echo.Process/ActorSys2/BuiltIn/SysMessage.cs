using Echo.Config;

namespace Echo.ActorSys2.BuiltIn
{
    /// <summary>
    /// Base message type for all built-in processes
    /// </summary>
    internal abstract record SysMessage
    {
        public static readonly SysMessage Bootstrap = new BootstrapMsg();
    }

    /// <summary>
    /// Start up the process system
    /// </summary>
    internal record BootstrapMsg : SysMessage;

}