using System;

namespace Echo
{
    [Flags]
    public enum InboxDirective
    {
        Default = 0,
        PushToFrontOfQueue = 1,
        Pause = 2,
        Shutdown = 4
    }
}
