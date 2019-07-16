using System;
using LanguageExt;
using static Echo.Process;
using static LanguageExt.Prelude;
using Echo.Client;

namespace Echo
{
    public static class ProcessHub
    {
        const int ConnectionCutOffMinutes = 5;

        static readonly object sync = new object();
        static Func<ProcessId, bool> routeValidator = _ => false;
        static Map<ClientConnectionId, ClientConnection> connections = Map<ClientConnectionId, ClientConnection>();

        /// <summary>
        /// Ctor
        /// </summary>
        static ProcessHub()
        {
            // Runs every minute checking for orphaned connections
            Monitor();
        }

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
        /// Monitor open connections that should be closed
        /// </summary>
        static void Monitor()
        {
            try
            {
                var conns = Connections;

                foreach(var conn in conns)
                {
                    if((DateTime.UtcNow - conn.Value.LastAccess).TotalMinutes >= ConnectionCutOffMinutes)
                    {
                        try
                        {
                            CloseConnection(conn.Key);
                        }
                        catch (Exception e)
                        {
                            logErr(e);
                        }
                    }
                }
            }
            catch(Exception e)
            {
                logErr(e);
            }
            finally
            {
                delay(Monitor, 60 * seconds);
            }
        }

        /// <summary>
        /// Register a connection by ID
        /// </summary>
        /// <param name="id">Client connection ID</param>
        /// <param name="conn">Connection ID</param>
        public static ClientConnectionId OpenConnection(string remoteIp, Action<ClientMessageDTO> tell) =>
            EnsureConnection(ClientConnectionId.Generate(remoteIp), tell);

        /// <summary>
        /// Keep an existing connection alive
        /// </summary>
        public static ClientConnectionId EnsureConnection(ClientConnectionId id, Action<ClientMessageDTO> tell) =>
            connections.Find(id).Map(c => c.Id).IfNone(() =>
            {
                var conn = ClientConnection.New(id, tell);
                lock (sync)
                {
                    connections = connections.AddOrUpdate(id, conn);
                }
                return id;
            });

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
