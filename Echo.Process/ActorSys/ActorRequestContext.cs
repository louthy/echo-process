using Echo.Config;
using Echo.Session;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LanguageExt.Prelude;

namespace Echo
{
    class ActorRequestContext
    {
        public ActorItem Self => self();
        readonly Func<ActorItem> self;
        public readonly ProcessId Sender;
        public readonly ActorItem Parent;
        public readonly ActorSystem System;

        public object CurrentMsg;
        public ActorRequest CurrentRequest;
        public ProcessFlags ProcessFlags;

        public ActorRequestContext(
            ActorSystem system,
            Func<ActorItem> self,
            ProcessId sender,
            ActorItem parent,
            object currentMsg,
            ActorRequest currentRequest,
            ProcessFlags processFlags
            )
        {
            this.self = self;
            Sender = sender;
            Parent = parent;
            CurrentMsg = currentMsg;
            CurrentRequest = currentRequest;
            ProcessFlags = processFlags;
            System = system;
        }

        public ActorRequestContext SetProcessFlags(ProcessFlags flags) =>
            new ActorRequestContext(
                System,
                self,
                Sender,
                Parent,
                CurrentMsg,
                CurrentRequest,
                flags
            );

        public ActorRequestContext SetCurrentRequest(ActorRequest currentRequest) =>
            new ActorRequestContext(
                System,
                self,
                Sender,
                Parent,
                CurrentMsg,
                currentRequest,
                ProcessFlags
            );

        public ActorRequestContext SetCurrentMessage(object currentMsg) =>
            new ActorRequestContext(
                System,
                self,
                Sender,
                Parent,
                currentMsg,
                CurrentRequest,
                ProcessFlags
            );

        public HashMap<string, ProcessId> Children =>
            Self.Actor.Children.Map(c => c.Actor.Id);
    }
}
