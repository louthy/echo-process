using LanguageExt;
using static LanguageExt.Prelude;
using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;

namespace Echo
{
    class Ping : IDisposable
    {
        static long Id;
        public readonly ActorSystem System;
        public readonly IDisposable Requests;
        public readonly IObservable<Resp> Responses;
        public readonly Subject<Resp> EarlyResponses = new Subject<Resp>();

        public Ping(ActorSystem sender)
        {
            System = sender;
            Responses = System.Cluster.Match(
                Some: c => c.SubscribeToChannel<Resp>("ping-responses"),
                None: () => Observable.Empty<Resp>())
               .Merge(EarlyResponses, TaskPoolScheduler.Default);

            Requests = System.Cluster.Match(
                Some: c => c.SubscribeToChannel<Req>("ping-requests"),
                None: () => Observable.Empty<Req>())
               .ObserveOn(TaskPoolScheduler.Default)
               .Subscribe(HandleRequest);
        }

        public class Req
        {
            public string SenderNode;
            public long RID;
            public string PID;
        }

        public class Resp
        {
            public string ReceiverNode;
            public long RID;
            public string PID;
            public bool Alive;
        }

        public void HandleRequest(Req req)
        {
            var pid = new ProcessId(req.PID);
            if (!System.IsLocal(pid)) return;
            var exists = System.GetDispatcher(req.PID).Exists;
            System.Cluster.Iter(c => c.PublishToChannel("ping-responses", new Resp()
            {
                PID = req.PID,
                RID = req.RID,
                ReceiverNode = req.SenderNode,
                Alive = exists
            }));
        }

        public IObservable<bool> DoPing(ProcessId pid, TimeSpan span = default(TimeSpan))
        {
            var req = new Req
            {
                PID = pid.ToString(),
                RID = Interlocked.Increment(ref Id),
                SenderNode = System.Name.Value
            };

            return (from r in Responses
                    where r.PID == req.PID && r.RID == req.RID && r.ReceiverNode == req.SenderNode
                    select r.Alive)
                   .Take(1)
                   .Timeout(span.Seconds == 0
                        ? TimeSpan.FromSeconds(1)
                        : span)
                   .Catch(Observable.Return(false))
                   .ObserveOn(TaskPoolScheduler.Default)
                   .PostSubscribe(() =>
                   {
                       System.Cluster.Match(
                           Some: c =>
                           {
                               if (c.PublishToChannel("ping-requests", req) == 0)
                               {
                                   // The message didn't reach any endpoints, so let's early out 
                                   EarlyResponses.OnNext(new Resp()
                                   {
                                       PID = req.PID,
                                       RID = req.RID,
                                       ReceiverNode = req.SenderNode
                                   });
                               }
                           },
                           None:() => EarlyResponses.OnNext(new Resp()
                                      {
                                          PID = req.PID,
                                          RID = req.RID,
                                          ReceiverNode = req.SenderNode
                                      })
                      );
                   });
        }

        public void Dispose()
        {
            System.Cluster.Iter(c => c.UnsubscribeChannel("ping-responses"));
            Requests.Dispose();
        }
    }
}
