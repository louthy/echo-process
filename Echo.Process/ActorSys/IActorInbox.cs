using LanguageExt;
using System;

namespace Echo
{
    interface IActorInbox : IDisposable
    {
        Unit Startup(IActor process, ActorItem parent, bool cluster, int maxMailboxSize);
        Unit Pause();
        Unit Unpause();
        Unit Shutdown();
        bool IsPaused { get; }
    }
}
