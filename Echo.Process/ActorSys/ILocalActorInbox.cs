using LanguageExt;
using System;
using System.Collections.Generic;

namespace Echo
{
    internal interface ILocalActorInbox
    {
        Unit Ask(object message, ProcessId sender, Option<SessionId> sessionId);
        Unit Tell(object message, ProcessId sender, Option<SessionId> sessionId);
        Unit TellUserControl(UserControlMessage message, Option<SessionId> sessionId);
        Unit TellSystem(SystemMessage message);
        object ValidateMessageType(object message, ProcessId sender);
        Either<string, bool> CanAcceptMessageType<TMsg>();
        Either<string, bool> HasStateTypeOf<TState>();
        int Count { get; }
        IEnumerable<Type> GetValidMessageTypes();
    }
}
