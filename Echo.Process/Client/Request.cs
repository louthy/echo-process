using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using static LanguageExt.Prelude;
using static LanguageExt.Process;
using Newtonsoft.Json;

namespace LanguageExt.Client
{
    public class Req
    {
        /// <summary>
        /// Parses a client request
        /// </summary>
        /// <param name="msg">Text version of the request
        /// 
        ///     procsys:conn
        ///     procsys:diss|<Connection ID>
        ///     procsys:tell|<Connection ID>|<Message ID>|<Process ID: To>|<Process ID: Sender>|<Message JSON>
        ///     procsys:ask|<Connection ID>|<Message ID>|<Process ID: To>|<Process ID: Sender>|<Message JSON>
        ///     procsys:ping|<Connection ID>
        ///     procsys:sub|<Connection ID>|<Process ID: Publisher>|<Process ID: Subscriber>
        ///     procsys:usub|<Connection ID>|<Process ID: Publisher>|<Process ID: Subscriber>
        /// 
        /// </param>
        /// <returns></returns>
        public static Either<string, Req> Parse(string msg, Map<ClientConnectionId, ClientConnection> activeConnections)
        {
            try
            {
                var parse = new BarParse(msg);

                return from hdr in parse.Header()
                       from typ in parse.GetNext()
                       from res in typ == "tell" ? from connid in parse.GetConnectionId()
                                                   from msgid  in parse.GetMessageId()
                                                   from to     in parse.GetProcessId()
                                                   from sender in parse.GetProcessId()
                                                   from body   in parse.GetNext()
                                                   select TellReq.Create(connid, msgid, to, sender, body, activeConnections)

                                : typ == "ask"   ? from connid in parse.GetConnectionId()
                                                   from msgid in parse.GetMessageId()
                                                   from to in parse.GetProcessId()
                                                   from sender in parse.GetProcessId()
                                                   from body in parse.GetNext()
                                                   select AskReq.Create(connid, msgid, to, sender, body, activeConnections)

                                : typ == "conn"  ? Right<string, Req>(ConnectReq.Default)

                                : typ == "diss"  ? DisconnectReq.Create(ClientConnectionId.New(parse.GetNextText()), activeConnections)

                                : typ == "sub"   ? from connid in parse.GetConnectionId()
                                                   from pub    in parse.GetProcessId()
                                                   from sub    in parse.GetProcessId()
                                                   select SubscribeReq.Create(connid, pub, sub, activeConnections)

                                : typ == "usub"  ? from connid in parse.GetConnectionId()
                                                   from pub    in parse.GetProcessId()
                                                   from sub    in parse.GetProcessId()
                                                   select UnSubscribeReq.Create(connid, pub, sub, activeConnections)

                                : typ == "ping"  ? PingReq.Create(ClientConnectionId.New(parse.GetNextText()), activeConnections)

                                : Left<string, Req>("Invalid request")
                       from req in res
                       select req;
            }
            catch(Exception e)
            {
                logErr(e);
                return "Invalid request";
            }
        }
    }

    public class ConnectReq : Req
    {
        public static readonly ConnectReq Default =
            new ConnectReq();
    }

    public class DisconnectReq : Req
    {
        public readonly ClientConnectionId Id;
        public readonly ClientConnection Connection;

        DisconnectReq(ClientConnectionId id, ClientConnection connection)
        {
            Id = id;
            Connection = connection;
        }

        public static Either<string, Req> Create(ClientConnectionId id, Map<ClientConnectionId, ClientConnection> clientConnections) =>
            clientConnections.Find(id).Map(x => new DisconnectReq(id, x) as Req)
                             .ToEither("Invalid connection-id");
    }

    public class PingReq : Req
    {
        public readonly ClientConnectionId Id;
        public readonly ClientConnection Connection;

        PingReq(ClientConnectionId id, ClientConnection connection)
        {
            Id = id;
            Connection = connection;
        }

        public static Either<string, Req> Create(ClientConnectionId id, Map<ClientConnectionId, ClientConnection> clientConnections) =>
            clientConnections.Find(id).Map(x => new PingReq(id, x) as Req)
                             .ToEither("Invalid connection-id");
    }

    public class TellReq : Req
    {
        public readonly ClientConnectionId Id;
        public readonly ClientMessageId MessageId;
        public readonly ProcessId To;
        public readonly ProcessId Sender;
        public readonly object Message;
        public readonly ClientConnection Connection;

        TellReq(ClientConnectionId id, ClientMessageId msgId, ProcessId to, ProcessId sender, object msg, ClientConnection connection)
        {
            Id = id;
            MessageId = msgId;
            To = to;
            Sender = sender;
            Message = msg;
            Connection = connection;
        }

        public static Either<string, Req> Create(ClientConnectionId id, ClientMessageId msgId, ProcessId to, ProcessId sender, string msg, Map<ClientConnectionId, ClientConnection> clientConnections)
        {
            if (!to.IsValid) return "Invalid process-id (To)"; 
            var conn = clientConnections.Find(id);
            if (conn.IsNone) return "Invalid connection-id";

            var msgObj = (from type in validMessageTypes(to)
                          let obj = JsonConvert.DeserializeObject(msg, type, ActorSystemConfig.Default.JsonSerializerSettings)
                          where obj != null
                          select obj)
                         .FirstOrDefault();

            if (msgObj == null) return "Message incompatible with the inbox message type";

            return new TellReq(id, msgId, to, sender, msgObj, conn.IfNone(() => null));
        }
    }

    public class AskReq : Req
    {
        // TODO: Consider merging AskReq and TellReq

        public readonly ClientConnectionId Id;
        public readonly ClientMessageId MessageId;
        public readonly ProcessId To;
        public readonly ProcessId Sender;
        public readonly object Message;
        public readonly ClientConnection Connection;

        AskReq(ClientConnectionId id, ClientMessageId msgId, ProcessId to, ProcessId sender, object msg, ClientConnection connection)
        {
            Id = id;
            MessageId = msgId;
            To = to;
            Sender = sender;
            Message = msg;
            Connection = connection;
        }

        public static Either<string, Req> Create(ClientConnectionId id, ClientMessageId msgId, ProcessId to, ProcessId sender, string msg, Map<ClientConnectionId, ClientConnection> clientConnections)
        {
            if (!to.IsValid) return "Invalid process-id (To)";
            var conn = clientConnections.Find(id);
            if (conn.IsNone) return "Invalid connection-id";

            var msgObj = (from type in validMessageTypes(to)
                          let obj = JsonConvert.DeserializeObject(msg, type, ActorSystemConfig.Default.JsonSerializerSettings)
                          where obj != null
                          select obj)
                         .FirstOrDefault();

            if (msgObj == null) return "Message incompatible with the inbox message type";

            return new AskReq(id, msgId, to, sender, msgObj, conn.IfNone(() => null));
        }
    }

    public class SubscribeReq : Req
    {
        public readonly ClientConnectionId Id;
        public readonly ProcessId Publisher;
        public readonly ProcessId Subscriber;
        public readonly ClientConnection Connection;

        SubscribeReq(ClientConnectionId id, ProcessId publisher, ProcessId subscriber, ClientConnection connection)
        {
            Id = id;
            Publisher = publisher;
            Subscriber = subscriber;
            Connection = connection;
        }

        public static Either<string, Req> Create(ClientConnectionId id, ProcessId publisher, ProcessId subscriber, Map<ClientConnectionId, ClientConnection> clientConnections)
        {
            if (!publisher.IsValid) return "Invalid publisher-id";
            if (!subscriber.IsValid) return "Invalid subscriber-id";
            var conn = clientConnections.Find(id);
            if (conn.IsNone) return "Invalid connection-id";
            return new SubscribeReq(id, publisher, subscriber, conn.IfNone(() => null));
        }
    }

    public class UnSubscribeReq : Req
    {
        public readonly ClientConnectionId Id;
        public readonly ProcessId Publisher;
        public readonly ProcessId Subscriber;
        public readonly ClientConnection Connection;

        UnSubscribeReq(ClientConnectionId id, ProcessId publisher, ProcessId subscriber, ClientConnection connection)
        {
            Id = id;
            Publisher = publisher;
            Subscriber = subscriber;
            Connection = connection;
        }

        public static Either<string, Req> Create(ClientConnectionId id, ProcessId publisher, ProcessId subscriber, Map<ClientConnectionId, ClientConnection> clientConnections)
        {
            if (!publisher.IsValid) return "Invalid publisher-id";
            if (!subscriber.IsValid) return "Invalid subscriber-id";
            var conn = clientConnections.Find(id);
            if (conn.IsNone) return "Invalid connection-id";
            return new UnSubscribeReq(id, publisher, subscriber, conn.IfNone(() => null));
        }
    }
}
