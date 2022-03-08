using System;
using System.Collections.Generic;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Echo
{
    class NullInbox : IActorInbox, ILocalActorInbox
    {
        public Unit Shutdown() =>
            unit;

        public Unit Startup(IActor pid, IActorSystem system, ActorItem parent, Option<ICluster> cluster, int maxMailboxSize) =>
            unit;

        public Unit Tell(object message, ProcessId sender) =>
            unit;

        public Unit Ask(object message, ProcessId sender, Option<SessionId> sessionId) =>
            unit;

        public Unit Tell(object message, ProcessId sender, Option<SessionId> sessionId) =>
            unit;

        public Unit TellUserControl(UserControlMessage message, Option<SessionId> sessionId) =>
            unit;

        public Unit TellSystem(SystemMessage message) => unit;
        
        public object ValidateMessageType(object message, ProcessId sender) =>
            message;

        public Either<string, bool> CanAcceptMessageType<TMsg>() =>
            false;

        public Either<string, bool> HasStateTypeOf<TState>() =>
            false;

        public int Count =>
            0;
        
        public IEnumerable<Type> GetValidMessageTypes() =>
            new Type [0];

        public Unit TellUserControl(UserControlMessage message) => unit;
        public Unit Pause() => unit;
        public Unit Unpause() => unit;
        public bool IsPaused => false;

        public void Dispose()
        {
        }
    }
}
