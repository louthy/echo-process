using System;
using System.Threading;
using System.Reflection;
using System.Reactive.Linq;
using static LanguageExt.Prelude;
using System.Reactive.Subjects;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Echo;
using Echo.Config;
using LanguageExt.ClassInstances;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;

namespace Echo
{
    /// <summary>
    /// Captures all state that can mutate during the life of an actor
    /// </summary>
    /// <remarks>By capturing all the mutable state, we can then use Atom to do atomic updates without the overhead
    /// of using the language-ext STM system.</remarks>
    /// <typeparam name="S">User's state type</typeparam>
    class ActorState<S>
    {
        /// <summary>
        /// Empty default state
        /// </summary>
        public static readonly ActorState<S> Default = 
            new ActorState<S>(default, default, default, Echo.StrategyState.Empty, default);

        /// <summary>
        /// Ctor
        /// </summary>
        public ActorState(HashMap<string, IDisposable> subs, HashMap<string, ActorItem> children, Option<S> state, StrategyState strategyState, Seq<IDisposable> remoteSubs)
        {
            Subs = subs;
            Children = children;
            State = state;
            StrategyState = strategyState;
            RemoteSubs = remoteSubs;
        }

        public readonly HashMap<string, IDisposable> Subs;
        public readonly HashMap<string, ActorItem> Children;
        public readonly Option<S> State;
        public readonly StrategyState StrategyState;
        public readonly Seq<IDisposable> RemoteSubs;

        /// <summary>
        /// Transform
        /// </summary>
        [Pure]
        public ActorState<S> With(HashMap<string, IDisposable>? Subs = null,
            HashMap<string, ActorItem>? Children = null,
            Option<S>? State = null,
            StrategyState StrategyState = null,
            Seq<IDisposable>? RemoteSubs = null) =>
            new ActorState<S>(
                Subs ?? this.Subs,
                Children ?? this.Children,
                State ?? this.State,
                StrategyState ?? this.StrategyState,
                RemoteSubs ?? this.RemoteSubs);

        /// <summary>
        /// Add a subscription
        /// </summary>
        /// <remarks>If one already exists then it is safely disposed</remarks>
        [Pure]
        public EffPure<ActorState<S>> AddSubscription(ProcessId pid, IDisposable sub) =>
            from self in RemoveSubscription(pid)
            select With(Subs: self.Subs.Add(pid.Path, sub));

        /// <summary>
        /// Remove a subscription and safely dispose it 
        /// </summary>
        [Pure]
        public EffPure<ActorState<S>> RemoveSubscription(ProcessId pid) =>
            from _ in Subs.Values.Map(DisposeEff).Sequence()
            select With(Subs: Subs.Remove(pid.Path));

        [Pure]
        static EffPure<Unit> DisposeEff(IDisposable d) =>
            Eff(() => { d.Dispose(); return unit; })
                .Match(SuccessEff, ProcessEff.logErr)
                .Flatten();

        /// <summary>
        /// Safely dispose and remove all subscriptions
        /// </summary>
        /// <returns></returns>
        [Pure]
        public EffPure<ActorState<S>> RemoveAllSubscriptions() =>
            from _ in Subs.Values.Map(DisposeEff).Sequence()
            select With(Subs: Empty);

        /// <summary>
        /// Disowns a child process
        /// </summary>
        [Pure]
        public ActorState<S> UnlinkChild(ProcessId pid) =>
            With(Children: Children.Remove(pid.Name.Value));

        /// <summary>
        /// Gains a child process
        /// </summary>
        [Pure]
        public Option<ActorState<S>> LinkChild(ActorItem item) =>
            Children.ContainsKey(item.Actor.Id.Name.Value)
                ? None
                : Some(With(Children: Children.AddOrUpdate(item.Actor.Id.Name.Value, item)));
        
        [Pure]
        public ActorState<S> ResetStrategyState() =>
            With(StrategyState: StrategyState.With(
                 Failures: 0,
                 FirstFailure: DateTime.MaxValue,
                 LastFailure: DateTime.MaxValue,
                 BackoffAmount: 0 * seconds));

        [Pure]
        public ActorState<S> SetStrategyState(StrategyState state) =>
            With(StrategyState: state);

        [Pure]
        public EffPure<ActorState<S>> Dispose() =>
            from _1 in RemoveAllSubscriptions()
            from _2 in DisposeRemoteSubs()
            from _3 in DisposeState()
            select With(RemoteSubs: Empty, Subs: Empty, State: None);

        [Pure]
        public EffPure<ActorState<S>> DisposeRemoteSubs() =>
            from _ in RemoteSubs.Map(DisposeEff).Sequence()
            select With(RemoteSubs: Empty);

        [Pure]
        public EffPure<ActorState<S>> DisposeState() =>
            from _ in State.Bind(x => x is IDisposable dx ? Some(dx) : None)
                           .Map(DisposeEff)
                           .Sequence()
            select With(State: None);
    }
}