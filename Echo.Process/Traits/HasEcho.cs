using System;
using LanguageExt;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo.Traits
{
    public interface EchoIO
    {
    }

    public interface HasEcho<RT> where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        EchoState<RT> EchoState { get; }
        RT MapEchoState(Func<EchoState<RT>, EchoState<RT>> f);
        Eff<RT, EchoIO> EchoEff { get; }
    }

    /// <summary>
    /// Echo-state system
    /// </summary>
    /// <remarks>
    ///  This is placeholder until the whole ActorContext system can be refactored
    /// </remarks>
    public class EchoState<RT> where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        internal static EchoState<RT> Default =>             
            new EchoState<RT>(
                ActorContext.SessionId,
                ActorContext.Request,
                ActorContext.DefaultSystem,
                ActorContext.Systems);

        internal EchoState(
            Option<SessionId> sessionId,
            ActorRequestContext request,
            ActorSystem system,
            SystemName[] systemNames)
        {
            SessionId   = sessionId;
            Request     = request;
            System      = system;
            System      = system;
            SystemNames = systemNames;
        }

        internal readonly Option<SessionId> SessionId;
        internal readonly ActorRequestContext Request;
        internal readonly ActorSystem System;
        internal readonly SystemName[] SystemNames;

        internal bool InMessageLoop =>
            Request != null;

        internal ProcessId Self => 
            InMessageLoop
                ? Request.Self.Actor.Id
                : System.User;

        internal ActorItem SelfProcess =>
            InMessageLoop
                ? Request.Self
                : System.UserContext.Self;
        
        internal ActorSystem GetSystem(ProcessId pid) =>
            GetSystem(pid.System);

        internal ActorSystem GetSystem(SystemName system) =>
            system.IsValid
                ? FindSystem(system) switch
                  {
                      null     => failwith<ActorSystem>($"Process-system does not exist {system}"),
                      var asys => asys
                  }
                : System;

        ActorSystem FindSystem(SystemName system) =>
            ActorContext.FindSystem(system);

        internal EchoState<RT> WithSystem(SystemName system) =>
            new EchoState<RT>(SessionId, Request, FindSystem(system) ?? throw new InvalidSystemNameException(), SystemNames);

    }
}