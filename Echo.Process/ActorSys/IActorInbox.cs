using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echo
{
    interface IActorInbox : IDisposable
    {
        Unit Startup(IActor process, ActorItem parent, Option<ICluster> cluster, int maxMailboxSize);
        Unit Pause();
        Unit Unpause();
        Unit Shutdown();
        bool IsPaused { get; }
    }
}
