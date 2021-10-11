using LanguageExt;
using System;

namespace Echo
{
    interface IActorInbox
    {
        Unit Startup(IActor process, IActorSystem system, ActorItem parent, Option<ICluster> cluster, int maxMailboxSize);
        Unit Pause();
        Unit Unpause();
        Unit Shutdown();
        bool IsPaused { get; }
    }
}
