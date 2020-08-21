using LanguageExt;
using System;
using System.Collections.Generic;

namespace Echo
{
    internal interface ILocalActorInbox : IActorInbox, IDisposable
    {
        Aff<RT, Unit> Ask<RT>(object message, ProcessId sender, Option<SessionId> sessionId) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> Tell<RT>(object message, ProcessId sender, Option<SessionId> sessionId) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> TellUserControl<RT>(UserControlMessage message, Option<SessionId> sessionId) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> TellSystem<RT>(SystemMessage message) where RT : struct, HasEcho<RT>;
        object ValidateMessageType<RT>(object message, ProcessId sender) where RT : struct, HasEcho<RT>;
        Aff<RT, Either<string, bool>> CanAcceptMessageType<RT, TMsg>() where RT : struct, HasEcho<RT>;
        Aff<RT, Either<string, bool>> HasStateTypeOf<RT, TState>() where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> Count<RT>() where RT : struct, HasEcho<RT>;
        Aff<RT, IEnumerable<Type>> GetValidMessageTypes<RT>() where RT : struct, HasEcho<RT>;
    }
}
