﻿using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using static LanguageExt.Prelude;
using static Echo.Process;
using LanguageExt;

namespace Echo
{
    internal class ActorDispatchNotExist : IActorDispatch
    {
        public readonly ProcessId ProcessId;

        public ActorDispatchNotExist(ProcessId pid)
        {
            ProcessId = pid;
        }

        private T Raise<T>(ProcessId sender) =>
            raise<T>(new ProcessException($"Doesn't exist ({ProcessId})", sender.Path, sender.Path, null));

        public HashMap<string, ProcessId> GetChildren() =>
            HashMap<string, ProcessId>();

        public IObservable<T> Observe<T>() =>
            Observable.Empty<T>();

        public IObservable<T> ObserveState<T>() =>
            Observable.Empty<T>();

        public Unit Tell(object message, Schedule schedule, ProcessId sender, Message.TagSpec tag) =>
            Raise<Unit>(sender);

        public Unit TellSystem(SystemMessage message, ProcessId sender) =>
            unit;

        public Unit TellUserControl(UserControlMessage message, ProcessId sender) =>
            Raise<Unit>(sender);

        public Unit Ask(object message, ProcessId sender) =>
            Raise<Unit>(sender);

        public Unit Publish(object message) =>
            Raise<Unit>(ProcessId.None);

        public Either<string, bool> CanAccept<T>() =>
            false;

        public Either<string, bool> HasStateTypeOf<T>() =>
            false;

        public Unit Kill() => 
            unit;

        public Unit Shutdown() =>
            unit;

        public int GetInboxCount() =>
            -1;

        public Unit Watch(ProcessId pid) =>
            Raise<Unit>(ProcessId.None);

        public Unit UnWatch(ProcessId pid) =>
            unit;

        public Unit DispatchWatch(ProcessId pid) =>
            Raise<Unit>(ProcessId.None);

        public Unit DispatchUnWatch(ProcessId pid) =>
            unit;

        public bool IsLocal => 
            false;

        public bool Exists =>
            false;

        public IEnumerable<Type> GetValidMessageTypes() =>
            new Type[0];

        public bool Ping() =>
            false;
    }
}
