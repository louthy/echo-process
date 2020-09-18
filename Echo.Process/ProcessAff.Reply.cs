using LanguageExt;
using System;
using static LanguageExt.Prelude;

namespace Echo
{
    /// <summary>
    /// <para>
    ///     Process: Reply functions
    /// </para>
    /// <para>
    ///     The reply functions are used to send responses back to processes that have sent
    ///     a message using 'ask'.  The replyError variants are for bespoke error handling
    ///     but if you let the process throw an exception when something goes wrong, the 
    ///     Process system will auto-reply with an error response (and throw it for the
    ///     process that's asking).  If the asking process doesn't capture the error then
    ///     it will continue to cascade to the original asking process.
    /// </para>
    /// </summary>
    public static partial class ProcessAff<RT>
        where RT : struct, HasEcho<RT>
    {
        /// <summary>
        /// Use this to cancel a reply in the proxy system
        /// </summary>
        public static readonly NoReturn noreply = 
            NoReturn.Default;

        /// <summary>
        /// Reply to an ask
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> reply<A>(A message) =>
            message is IReturn rmessage && !rmessage.HasValue
                ? unitEff
                : from aks in assertAsk
                  from req in ActorContextAff<RT>.Request
                  from res in ActorSystemAff<RT>.tell(
                                  req.CurrentRequest.ReplyTo, 
                                  new ActorResponse(
                                      message,
                                      req.CurrentRequest.ReplyTo, 
                                      req.Self.Actor.Id, 
                                      req.CurrentRequest.RequestId,
                                      typeof(A).AssemblyQualifiedName
                                  ),
                                  Schedule.Immediate,
                                  req.Self.Actor.Id)
                  select res;

        /// <summary>
        /// Reply if asked
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> replyIfAsked<A>(A message) =>
            isAsk.Bind(x => x ?  reply(message) : unitEff);

        /// <summary>
        /// Reply to an ask with an error
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> replyError(Exception exception) =>
            from aks in assertAsk
            from req in ActorContextAff<RT>.Request
            from res in ActorSystemAff<RT>.tell(
                            req.CurrentRequest.ReplyTo, 
                            new ActorResponse(
                                exception,
                                req.CurrentRequest.ReplyTo, 
                                req.Self.Actor.Id, 
                                req.CurrentRequest.RequestId,
                                exception.GetType().AssemblyQualifiedName),
                            Schedule.Immediate,
                            req.Self.Actor.Id)
            select res;            

        /// <summary>
        /// Reply to an ask with an error
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> replyError(string errorMessage) =>
            replyError(new Exception(errorMessage));

        /// <summary>
        /// Reply with an error if asked
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> replyErrorIfAsked(Exception exception) =>
            isAsk.Bind(x => x ?  replyError(exception) : unitEff);

        /// <summary>
        /// Reply with an error if asked
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> replyErrorIfAsked(string errorMessage) =>
            isAsk.Bind(x => x ?  replyError(errorMessage) : unitEff);

        /// <summary>
        /// Reply to the asker, or if it's not an ask then tell the sender
        /// via a message to their inbox.
        /// </summary>
        public static Aff<RT, Unit> replyOrTellSender<A>(A message) =>
            from aks in isAsk
            from res in aks ? reply(message) 
                            : message is IReturn rmsg && rmsg.HasValue
                                ? tell(Sender, message)
                                : unitEff
            select res;
    }
}
