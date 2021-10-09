using System;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo
{
    class NullInbox : IActorInbox
    {
        public Unit Shutdown() => unit;
        public Unit Startup(IActor pid, IActorSystem system, ActorItem parent, Option<ICluster> cluster, int maxMailboxSize) => unit;
        public Unit Tell(object message, ProcessId sender) => unit;
        public Unit TellSystem(SystemMessage message) => unit;
        public Unit TellUserControl(UserControlMessage message) => unit;
        public Unit Pause() => unit;
        public Unit Unpause() => unit;
        public bool IsPaused => false;

        public void Dispose()
        {
        }
    }
}
