using LanguageExt.UnitsOfMeasure;
using Newtonsoft.Json;
using System;
using static LanguageExt.Prelude;
using static Echo.Process;
using LanguageExt;

namespace Echo
{
    /// <summary>
    /// Process that monitors the state of the cluster
    /// </summary>
    class ClusterMonitor
    {
        public const string MembersKey = "sys-cluster-members";
        static readonly Time HeartbeatFreq = 1*seconds;
        static readonly Time OfflineCutoff = 4*seconds;

        public enum MsgTag
        {
            Heartbeat,
            ClusterMembersUpdated
        }

        public class Msg
        {
            public readonly MsgTag Tag;

            public Msg(MsgTag tag)
            {
                Tag = tag;
            }
        }

        public class State
        {
            public readonly AtomHashMap<ProcessName, ClusterNode> Members;
            public readonly IActorSystem System;

            public static State Create(AtomHashMap<ProcessName, ClusterNode> members, IActorSystem system) =>
                new State(members, system);

            State(AtomHashMap<ProcessName, ClusterNode> members, IActorSystem system)
            {
                members.FilterInPlace(node => node != null);
                Members = members;
                System  = system;
            }

            public State SetMember(ProcessName nodeName, ClusterNode state)
            {
                if (state == null)
                {
                    Members.Remove(nodeName);
                }
                else
                {
                    Members.AddOrUpdate(nodeName, state);
                }
                return this;
            }

            public State RemoveMember(ProcessName nodeName)
            {
                Members.Remove(nodeName);
                return this;
            }
        }

        /// <summary>
        /// Root Process setup
        /// </summary>
        public static State Setup(State state)
        {
            SelfHeartbeat();
            return Heartbeat(state, state.System.Cluster);
        }

        /// <summary>
        /// Root Process inbox
        /// </summary>
        public static State Inbox(State state, Msg msg)
        {
            switch (msg.Tag)
            {
                case MsgTag.Heartbeat:
                    try
                    {
                        return Heartbeat(state, state.System.Cluster);
                    }
                    catch(Exception e)
                    {
                        logErr(e);
                    }
                    finally
                    {
                        SelfHeartbeat();
                    }
                    break;
            }

            return state;
        }

        static Unit SelfHeartbeat() =>
            tellSelf(new Msg(MsgTag.Heartbeat), Schedule.Ephemeral(HeartbeatFreq, "heartbeat"));

        /// <summary>
        /// If this node is part of a cluster then it updates a shared map of 
        /// node-names to states.  This also downloads the latest map so the
        /// cluster state is known locally.
        /// </summary>
        /// <param name="state">Current state</param>
        /// <param name="cluster">Cluster connection</param>
        /// <returns>Latest state from the cluster, or a map with just one item 'root'
        /// in it that represents this node.</returns>
        static State Heartbeat(State state, Option<ICluster> cluster) =>
            cluster.Map(
                c =>
                {
                    try
                    {
                        var cutOff = DateTime.UtcNow.Add(0 * seconds - OfflineCutoff);
                        c.HashFieldAddOrUpdate(MembersKey, c.NodeName.Value, new ClusterNode(c.NodeName, DateTime.UtcNow, c.Role));

                        state.Members.Swap(
                            oldState => {
                                var newState = c.GetHashFields<ProcessName, ClusterNode>(MembersKey, s => new ProcessName(s))
                                                .Where(m => m.LastHeartbeat > cutOff);

                                var (offline, online) = DiffState(oldState, newState);

                                offline.Iter(offline => publish(state.Members[offline]));
                                online.Iter(online => publish(newState[online]));

                                return newState;
                            });

                        return state;
                    }
                    catch(Exception e)
                    {
                        logErr(e);
                        return HeartbeatLocal(state);
                    }
                })
            .IfNone(HeartbeatLocal(state));

        static (HashSet<ProcessName> Offline, HashSet<ProcessName> Online) DiffState(
            HashMap<ProcessName, ClusterNode> oldState, 
            HashMap<ProcessName, ClusterNode> newState)
        {
            var oldSet = toHashSet(oldState.Keys);
            var newSet = toHashSet(newState.Keys);
            return (oldSet - newSet, newSet - oldSet);
        }

        static State HeartbeatLocal(State state) =>
            state.SetMember("root", new ClusterNode("root", DateTime.UtcNow, "local"));

        static string GetNodeName(Option<ICluster> cluster) =>
            cluster.Map(c => c.NodeName.Value).IfNone("root");
    }
}
