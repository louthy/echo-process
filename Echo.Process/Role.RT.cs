using System;
using LanguageExt.ClassInstances;
using System.Collections.Generic;
using System.Linq;
using Echo.Traits;
using static LanguageExt.Prelude;
using static Echo.Process;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Echo
{
    /// <summary>
    /// <para>
    /// Each node in the cluster has a role name and at all times the cluster-nodes
    /// have a list of the alive nodes and their roles (Process.ClusterNodes).  Nodes 
    /// are removed from Process.ClusterNodes if they don't phone in. Process.ClusterNodes 
    /// is at most 3 seconds out-of-date and can therefore be used to reliably find
    /// out which nodes are available and what roles they do.  
    /// </para>
    /// <para>
    /// By using Role.First, Role.Broadcast, Role.LeastBusy, Role.Random and Role.RoundRobin
    /// you can build a ProcessId that is resolved at the time of doing a 'tell', 'ask',
    /// 'subscribe', etc.  This can allow reliable messaging to Processes in the cluster.
    /// </para>
    /// </summary>
    public static class Role<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        /// <summary>
        /// The role that this node is a part of
        /// </summary>
        public static Eff<RT, ProcessName> Current =>
            SuccessEff(Role.Current);

        /// <summary>
        /// A ProcessId that represents a set of nodes in a cluster.  When used for
        /// operations like 'tell', the message is dispatched to all nodes in the set.
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// You may create a reference to child nodes in the usual way:
        ///     Role.Broadcast["my-role"]["user"]["child-1"][...]
        /// </remarks>
        /// <example>
        ///     tell( Role.Broadcast["message-role"]["user"]["message-log"], "Hello" );
        /// </example>
        public static Eff<RT, ProcessId> Broadcast =>
            SuccessEff(Role.Broadcast);

        /// <summary>
        /// A ProcessId that represents a set of nodes in a cluster.  When used for
        /// operations like 'tell', the message is dispatched to the least busy from
        /// the set.
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// You may create a reference to child nodes in the usual way:
        ///     Role.LeastBusy["my-role"]["user"]["child-1"][...]
        /// </remarks>
        /// <example>
        ///     tell( Role.LeastBusy["message-role"]["user"]["message-log"], "Hello" );
        /// </example>
        public static Eff<RT, ProcessId> LeastBusy =>
            SuccessEff(Role.LeastBusy);

        /// <summary>
        /// A ProcessId that represents a set of nodes in a cluster.  When used for
        /// operations like 'tell', the message is dispatched to a cryptographically
        /// random node from the set.
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// You may create a reference to child nodes in the usual way:
        ///     Role.Random["my-role"]["user"]["child-1"][...]
        /// </remarks>
        /// <example>
        ///     tell( Role.Random["message-role"]["user"]["message-log"], "Hello" );
        /// </example>
        public static Eff<RT, ProcessId> Random =>
            SuccessEff(Role.Random);

        /// <summary>
        /// A ProcessId that represents a set of nodes in a cluster.  When used for
        /// operations like 'tell', the message is dispatched to the nodes in a round-
        /// robin fashion
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// You may create a reference to child nodes in the usual way:
        ///     Role.RoundRobin["my-role"]["user"]["child-1"][...]
        /// </remarks>
        /// <example>
        ///     tell( Role.RoundRobin["message-role"]["user"]["message-log"], "Hello" );
        /// </example>
        public static Eff<RT, ProcessId> RoundRobin =>
            SuccessEff(Role.RoundRobin);

        /// <summary>
        /// A ProcessId that represents a set of nodes in a cluster.  When used for 
        /// operations like 'tell', the node names are sorted in ascending order and 
        /// the message is dispatched to the first one.  This can be used for leader
        /// election for example.
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// You may create a reference to child nodes in the usual way:
        ///     Role.First["my-role"]["user"]["child-1"][...]
        /// </remarks>
        /// <example>
        ///     tell( Role.First["message-role"]["user"]["message-log"], "Hello" );
        /// </example>
        public static Eff<RT, ProcessId> First =>
            SuccessEff(Role.First);

        /// <summary>
        /// A ProcessId that represents a set of nodes in a cluster.  When used for 
        /// operations like 'tell', the node names are sorted in ascending order and 
        /// the message is dispatched to the second one.
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// You may create a reference to child nodes in the usual way:
        ///     Role.Second["my-role"]["user"]["child-1"][...]
        /// </remarks>
        /// <example>
        ///     tell( Role.Second["message-role"]["user"]["message-log"], "Hello" );
        /// </example>
        public static Eff<RT, ProcessId> Second =>
            SuccessEff(Role.Second);

        /// <summary>
        /// A ProcessId that represents a set of nodes in a cluster.  When used for 
        /// operations like 'tell', the node names are sorted in ascending order and 
        /// the message is dispatched to the third one.
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// You may create a reference to child nodes in the usual way:
        ///     Role.Third["my-role"]["user"]["child-1"][...]
        /// </remarks>
        /// <example>
        ///     tell( Role.Third["message-role"]["user"]["message-log"], "Hello" );
        /// </example>
        public static Eff<RT, ProcessId> Third =>
            SuccessEff(Role.Third);

        /// <summary>
        /// A ProcessId that represents a set of nodes in a cluster.  When used for 
        /// operations like 'tell', the node names are sorted in descending order and 
        /// the message is dispatched to the first one.
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// You may create a reference to child nodes in the usual way:
        ///     Role.Last["my-role"]["user"]["child-1"][...]
        /// </remarks>
        /// <example>
        ///     tell( Role.Last["message-role"]["user"]["message-log"], "Hello" );
        /// </example>
        public static Eff<RT, ProcessId> Last =>
            SuccessEff(Role.Last);

        /// <summary>
        /// Builds a ProcessId that represents the next node in the role that this node
        /// is a part of.  If there is only one node in the role then any messages sent
        /// will be sent to the leaf-process with itself.  Unlike other Roles, you do 
        /// not specify the role-name as the first child. 
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// You may create a reference to child nodes in the usual way:
        ///     Role.Next["user"]["child-1"][...]
        /// </remarks>
        /// <example>
        ///     tell( Role.Next["user"]["message-log"], "Hello" );
        /// </example>
        public static Eff<RT, ProcessId> Next =>
            Process<RT>.CurrentSystem.Map(Role.Next);

        /// <summary>
        /// Builds a ProcessId that represents the previous node in the role that this 
        /// node is a part of.  If there is only one node in the role then any messages 
        /// sent will be sent to the leaf-process with itself.  Unlike other Roles, you 
        /// do not specify the role-name as the first child. 
        /// See remarks.
        /// </summary>
        /// <remarks>
        /// You may create a reference to child nodes in the usual way:
        ///     Role.Prev["user"]["child-1"][...]
        /// </remarks>
        /// <example>
        ///     tell( Role.Prev["user"]["message-log"], "Hello" );
        /// </example>
        public static Eff<RT, ProcessId> Prev =>
            Process<RT>.CurrentSystem.Map(Role.Prev);

        public static Eff<RT, Seq<ProcessId>> NodeIds(ProcessId leaf) =>
            SuccessEff(Role.NodeIds(leaf).ToSeq());

        public static Eff<RT, HashMap<ProcessName, ClusterNode>> Nodes(ProcessId leaf) =>
            Process<RT>.CurrentSystem.Map(sys => Role.Nodes(leaf, sys));
    }
}
