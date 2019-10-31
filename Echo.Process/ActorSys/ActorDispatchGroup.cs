﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reactive.Linq;
using LanguageExt;

namespace Echo
{
    class ActorDispatchGroup : IActorDispatch
    {
        readonly ProcessId[] group;
        readonly int count;
        readonly bool transactionalIO;

        public ActorDispatchGroup(IEnumerable<ProcessId> group, bool transactionalIO)
        {
            this.group = group.ToArray();
            this.transactionalIO = transactionalIO;
            this.count = this.group.Length;
            if( this.group.Length == 0 )
            {
                throw new ArgumentException("No processes in group - usually this means there are offline services.");
            }
        }

        private IEnumerable<IActorDispatch> GetWorkers() =>
            group.Map(pid => ActorContext.System(pid).GetDispatcher(pid));

        private Unit IterRoleMembers(Action<IActorDispatch> action) =>
            GetWorkers().Iter(action);

        private IEnumerable<R> MapRoleMembers<R>(Func<IActorDispatch, R> map) =>
            count > 1
                ? GetWorkers().Map(map).AsParallel()
                : GetWorkers().Map(map);

        public Unit Ask(object message, ProcessId sender) =>
            IterRoleMembers(d => d.Ask(message, sender));

        public Unit DispatchUnWatch(ProcessId pid) =>
            IterRoleMembers(d => d.DispatchUnWatch(pid));

        public Unit DispatchWatch(ProcessId pid) =>
            IterRoleMembers(d => d.DispatchWatch(pid));

        public HashMap<string, ProcessId> GetChildren() =>
            List.fold(
                MapRoleMembers(disp => disp.GetChildren()),
                HashMap.empty<string, ProcessId>(), 
                (s, x) => s + x
                );

        public int GetInboxCount() =>
            List.fold(MapRoleMembers(disp => disp.GetInboxCount()), 0, (s, x) => s + x);

        public Either<string, bool> CanAccept<T>() =>
            List.fold(MapRoleMembers(disp => disp.CanAccept<T>()), true, (s, x) => s && x.IsRight);

        public Either<string, bool> HasStateTypeOf<T>() =>
            List.fold(MapRoleMembers(disp => disp.HasStateTypeOf<T>()), true, (s, x) => s && x.IsRight);

        public Unit Kill() =>
            IterRoleMembers(d => d.Kill());

        public Unit Shutdown() =>
            IterRoleMembers(d => d.Shutdown());

        public IObservable<T> Observe<T>() =>
            Observable.Merge(MapRoleMembers(disp => disp.Observe<T>()));

        public IObservable<T> ObserveState<T>() =>
            Observable.Merge(MapRoleMembers(disp => disp.ObserveState<T>()));

        public Unit Publish(object message) =>
            IterRoleMembers(d => d.Publish(message));

        public Unit Tell(object message, Schedule schedule, ProcessId sender, Message.TagSpec tag) =>
            IterRoleMembers(d => d.Tell(message, schedule, sender, tag));

        public Unit TellSystem(SystemMessage message, ProcessId sender) =>
            IterRoleMembers(d => d.TellSystem(message, sender));

        public Unit TellUserControl(UserControlMessage message, ProcessId sender) =>
            IterRoleMembers(d => d.TellUserControl(message, sender));

        public Unit UnWatch(ProcessId pid) =>
            IterRoleMembers(d => d.UnWatch(pid));

        public Unit Watch(ProcessId pid) =>
            IterRoleMembers(d => d.Watch(pid));

        public bool IsLocal => 
            false;

        public bool Exists =>
            MapRoleMembers(disp => disp).Exists(x => x.Exists);

        public IEnumerable<Type> GetValidMessageTypes() =>
            from x in MapRoleMembers(disp => disp.GetValidMessageTypes())
            from y in x
            select y;

        public bool Ping() =>
            Exists && MapRoleMembers(disp => disp.Ping()).Exists(x => x);
    }
}