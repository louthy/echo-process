using System;
using LanguageExt;
using static LanguageExt.Process;
using static LanguageExt.Prelude;
using LanguageExt.Client;

namespace LanguageExt
{
    public static class ProcessHub
    {
        static readonly object sync = new object();
        static Func<ProcessId, bool> routeValidator = _ => false;
        static Map<ClientConnectionId, ClientConnection> connections = Map.empty<ClientConnectionId, ClientConnection>();

        /// <summary>
        /// Inject a function that validates incoming message destinations 
        /// </summary>
        /// <param name="routePredicate">Takes the To of a message and returns True if the message is
        /// allowed to pass.</param>
        public static Func<ProcessId, bool> RouteValidator
        {
            get => routeValidator;
            set => routeValidator = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Active client connections 
        /// </summary>
        public static Map<ClientConnectionId, ClientConnection> Connections => connections;

        /// <summary>
        /// Register a connection by ID
        /// </summary>
        /// <param name="id">Client connection ID</param>
        /// <param name="conn">Connection ID</param>
        public static ClientConnectionId OpenConnection(Action<ClientMessageDTO> tell)
        {
            var id = ClientConnectionId.Generate();
            var conn = ClientConnection.New(id, tell);
            lock (sync)
            {
                connections = connections.AddOrUpdate(id, conn);
            }
            return id;
        }

        /// <summary>
        /// Register a connection by ID
        /// </summary>
        /// <param name="id">Client connection ID</param>
        /// <param name="conn">Connection ID</param>
        public static Unit CloseConnection(ClientConnectionId id)
        {
            connections.Find(id).Iter(conn => conn.Dispose());
            lock (sync)
            {
                connections = connections.Remove(id);
            }
            return unit;
        }

        /// <summary>
        /// Confirm the client is still there
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>True if the connection is still active</returns>
        public static bool TouchConnection(ClientConnectionId id) =>
            connections.Find(id).Map(c => c.Touch()).Map(_ => true).IfNone(false);

        /// <summary>
        /// Subscribe 
        /// </summary>
        public static bool Subscribe(ClientConnectionId id, ProcessId pub, ProcessId sub) =>
            connections.Find(id).Map(c => c.AddSubscriber(pub, sub)).Map(_ => true).IfNone(false);

        /// <summary>
        /// Un-subscribe 
        /// </summary>
        public static bool UnSubscribe(ClientConnectionId id, ProcessId pub, ProcessId sub) =>
            connections.Find(id).Map(c => c.RemoveSubscriber(pub, sub)).Map(_ => true).IfNone(false);
    }
}
