using LanguageExt;
using System;
using System.Collections.Generic;

namespace Echo
{
    interface IActorDispatch
    {
        Aff<RT, IObservable<A>> Observe<RT, A>() where RT : struct, HasEcho<RT>;
        Aff<RT, IObservable<A>> ObserveState<RT, A>() where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> Ask<RT>(object message, ProcessId sender) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> Tell<RT>(object message, Schedule schedule, ProcessId sender, Message.TagSpec tag) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> TellSystem<RT>(SystemMessage message, ProcessId sender) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> TellUserControl<RT>(UserControlMessage message, ProcessId sender) where RT : struct, HasEcho<RT>;
        Aff<RT, HashMap<string, ProcessId>> GetChildren<RT>() where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> Publish<RT>(object message) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> Kill<RT>() where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> Shutdown<RT>() where RT : struct, HasEcho<RT>;
        Aff<RT, int> GetInboxCount<RT>() where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> Watch<RT>(ProcessId pid) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> UnWatch<RT>(ProcessId pid) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> DispatchWatch<RT>(ProcessId pid) where RT : struct, HasEcho<RT>;
        Aff<RT, Unit> DispatchUnWatch<RT>(ProcessId pid) where RT : struct, HasEcho<RT>;
        Aff<RT, bool> IsLocal<RT>() where RT : struct, HasEcho<RT>
        Aff<RT, bool> Exists<RT>() where RT : struct, HasEcho<RT>
        Aff<RT, bool> CanAccept<RT, A>() where RT : struct, HasEcho<RT>;
        Aff<RT, bool> HasStateTypeOf<RT, A>() where RT : struct, HasEcho<RT>;
        IEnumerable<Type> GetValidMessageTypes<RT>() where RT : struct, HasEcho<RT>;
        Aff<RT, bool> Ping<RT>() where RT : struct, HasEcho<RT>;
    }
}
