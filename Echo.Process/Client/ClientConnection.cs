using System;
using System.Text;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using LanguageExt.ClassInstances.Const;
using LanguageExt.ClassInstances.Pred;
using static LanguageExt.Prelude;
using System.Security.Cryptography;
using LanguageExt;

namespace Echo.Client
{
    public class ClientMessageId : NewType<ClientMessageId, long> { public ClientMessageId(long x) : base(x) { } }

    public class ClientConnectionId : NewType<ClientConnectionId, string, StrLen<I10,I100>>
    {
        readonly static RandomNumberGenerator rnd = RandomNumberGenerator.Create();
        const int DefaulteSizeInBytes = 32;
        
        public ClientConnectionId(string value) : base(value)
        { }

        public static explicit operator String(ClientConnectionId id) =>
            id.Value;

        /// <summary>
        /// Generate 
        /// </summary>
        /// <returns></returns>
        public static ClientConnectionId Generate(string remoteIp)
        {
            var bytes = new byte[DefaulteSizeInBytes];
            rnd.GetBytes(bytes);
            var id = new StringBuilder(DefaulteSizeInBytes);
            foreach(var b in bytes)
            {
                id.Append((char)('a' + (b % 26)));
            }
            return New($"{remoteIp.GetHashCode()}-{id}");
        }
    }

    public class ClientConnection : IDisposable
    {
        public readonly ClientConnectionId Id;
        public readonly Action<ClientMessageDTO> Tell;
        object sync = new object();
        HashMap<ProcessId, Lst<Subscriber>> subscriptions;
        DateTime lastAccess;

        public static ClientConnection New(ClientConnectionId id, Action<ClientMessageDTO> tell) =>
            new ClientConnection(id, HashMap<ProcessId, Lst<Subscriber>>(), DateTime.UtcNow, tell);

        ClientConnection(ClientConnectionId id, HashMap<ProcessId, Lst<Subscriber>> subscriptions, DateTime lastAccess, Action<ClientMessageDTO> tell)
        {
            Id = id;
            this.subscriptions = subscriptions;
            this.lastAccess = lastAccess;
            Tell = tell;
        }

        public DateTime LastAccess => 
            lastAccess;

        public ClientConnection Touch()
        {
            lastAccess = DateTime.UtcNow;
            return this;
        }

        public ClientConnection AddSubscriber(ProcessId publisher, ProcessId subscriber)
        {
            lock (sync)
            {
                var isSubbed = subscriptions.Find(publisher)
                                            .Map(list => list.Exists(x => x.Equals(subscriber)))
                                            .IfNone(false);

                if (isSubbed) return this;

                var disp = Process.observe<object>(publisher).Where(msg => msg != null).Subscribe(msg =>
                    Tell(new ClientMessageDTO
                    {
                        connectionId = (string)Id,
                        content = msg,
                        contentType = msg.GetType().AssemblyQualifiedName,
                        replyTo = publisher.Path,
                        requestId = 0L,
                        sender = publisher.Path,
                        sessionId = "",
                        tag = "tell",
                        to = subscriber.Path,
                        type = Message.Type.User.ToString()
                    }));

                var sub = new Subscriber(subscriber, disp);

                subscriptions = subscriptions.AddOrUpdate(
                    publisher,
                    Some: subs => subs.Add(sub),
                    None: ()   => List(sub));
            }

            Touch();
            return this;
        }

        public ClientConnection RemoveSubscriber(ProcessId publisher, ProcessId subscriber)
        {
            lock (sync)
            {
                // Dispose of the subscriptions first
                subscriptions.Find(publisher)
                             .Iter(subs => subs.Filter(sub => sub.Equals(subscriber))
                                               .Iter(sub => sub.Dispose()));

                // Then remove them
                subscriptions = subscriptions.SetItem(publisher, Some: subs => subs.RemoveAll(s => s.Equals(subscriber)));

                if (subscriptions[publisher].Count == 0) subscriptions = subscriptions.Remove(publisher);
            }
            Touch();
            return this;
        }

        public void Dispose()
        {
            foreach(var subs in subscriptions.Values)
            {
                foreach(var sub in subs)
                {
                    sub.Dispose();
                }
            }
        }

        class Subscriber : IEquatable<Subscriber>, IComparable<Subscriber>, IEquatable<ProcessId>, IComparable<ProcessId>, IDisposable
        {
            public readonly ProcessId Id;
            public readonly IDisposable Sub;

            public Subscriber(ProcessId id, IDisposable sub)
            {
                Id = id;
                Sub = sub;
            }

            public int CompareTo(ProcessId other) =>
                Id.CompareTo(other);

            public int CompareTo(Subscriber other) =>
                Id.CompareTo(other.Id);

            public void Dispose() =>
                Sub.Dispose();

            public bool Equals(Subscriber other) =>
                Id == other.Id;

            public bool Equals(ProcessId other) =>
                Id == other;

            public override bool Equals(object obj) =>
                !ReferenceEquals(obj, null) &&
                obj is Subscriber &&
                Equals((Subscriber)obj);

            public override int GetHashCode() =>
                Id.GetHashCode();
        }
    }
}
