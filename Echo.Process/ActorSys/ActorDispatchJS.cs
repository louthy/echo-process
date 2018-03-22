using Echo.Client;
using LanguageExt;
using static LanguageExt.Prelude;
using System;
using System.Collections.Generic;
using static Echo.Process;

namespace Echo
{
    internal class ActorDispatchJS : IActorDispatch
    {
        public readonly ProcessId ProcessId;
        public readonly ClientConnectionId Id;
        public readonly Option<SessionId> SessionId;
        readonly bool transactionalIO;

        public ActorDispatchJS(ProcessId pid, Option<SessionId> sessionId, bool transactionalIO)
        {
            Id = ClientConnectionId.New(pid.Skip(2).Take(1).Name.Value);
            ProcessHub.Connections.Find(Id).IfNone(() => { throw new ClientDisconnectedException(Id); });
            
            ProcessId = pid;
            SessionId = sessionId;
            this.transactionalIO = transactionalIO;
        }

        public Map<string, ProcessId> GetChildren()
        {
            throw new NotImplementedException();
        }

        public IObservable<T> Observe<T>()
        {
            throw new NotImplementedException();
        }

        public IObservable<T> ObserveState<T>()
        {
            throw new NotImplementedException();
        }

        public int GetInboxCount() => -1;

        public Unit Tell(object message, Schedule schedule, ProcessId sender, Message.TagSpec tag) =>
            LocalScheduler.Push(schedule, ProcessId, m => Tell(m, sender, "tell", Message.Type.User), message);

        public Unit TellSystem(SystemMessage message, ProcessId sender) =>
            Tell(message, sender, "sys-"+message.Tag.ToString().ToLower(), Message.Type.System);

        public Unit TellUserControl(UserControlMessage message, ProcessId sender) =>
            Tell(message, sender, "usr-" + message.Tag.ToString().ToLower(), Message.Type.UserControl);

        Unit Tell(object message, ProcessId sender, string inbox, Message.Type type) =>
            ProcessHub.Connections.Find(Id).Iter(c => c.Tell(
                new ClientMessageDTO
                {
                    tag = inbox,
                    type = type.ToString().ToLower(),
                    connectionId = (string)Id,
                    content = message,
                    contentType = message.GetType().AssemblyQualifiedName,
                    sender = sender.Path,
                    replyTo = sender.Path,
                    requestId = 0,
                    sessionId = SessionId.Map(x => x.ToString()).IfNone(""),
                    to = ProcessId.Skip(3).Path
                }));

        public Unit Ask(object message, ProcessId sender)
        {
            var req = (ActorRequest)message;

            return ProcessHub.Connections.Find(Id).Iter(c => c.Tell(
                new ClientMessageDTO
                {
                    tag = "ask",
                    type = Message.Type.User.ToString().ToLower(),
                    connectionId = (string)Id,
                    content = req.Message,
                    contentType = req.Message.GetType().AssemblyQualifiedName,
                    sender = sender.Path,
                    replyTo = sender.Path,
                    requestId = req.RequestId,
                    sessionId = SessionId.Map(x => x.ToString()).IfNone(""),
                    to = ProcessId.Skip(3).Path
                }));
        }

        public Unit Publish(object message)
        {
            throw new NotSupportedException();
        }

        public Either<string, bool> CanAccept<T>() => true;
        public Either<string, bool> HasStateTypeOf<T>() => true;

        public Unit Kill() =>
            // TODO: Not yet implemented on the JS side
            ProcessId.Tell(SystemMessage.ShutdownProcess(false), Self);

        public Unit Shutdown() =>
            // TODO: Not yet implemented on the JS side
            ProcessId.Tell(SystemMessage.ShutdownProcess(true), Self);

        public Unit Watch(ProcessId pid)
        {
            throw new NotSupportedException();
        }

        public Unit UnWatch(ProcessId pid)
        {
            throw new NotSupportedException();
        }

        public Unit DispatchWatch(ProcessId pid)
        {
            throw new NotSupportedException();
        }

        public Unit DispatchUnWatch(ProcessId pid)
        {
            throw new NotSupportedException();
        }

        public bool IsLocal =>
            false;

        public bool Exists =>
            Prelude.raise<bool>(new NotSupportedException());

        public IEnumerable<Type> GetValidMessageTypes() =>
            new Type[0];

        public bool Ping() =>
            throw new NotSupportedException();
    }
}
