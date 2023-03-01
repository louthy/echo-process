using LanguageExt;
using System;
using static LanguageExt.Prelude;

namespace Echo;

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
public static partial class Process<RT>
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
    public static Eff<RT, Unit> reply<A>(A message) =>
        Eff<RT, Unit>(_ => Process.reply(message));

    /// <summary>
    /// Reply if asked
    /// </summary>
    /// <remarks>
    /// This should be used from within a process' message loop only
    /// </remarks>
    public static Eff<RT, Unit> replyIfAsked<A>(A message) =>
        Eff<RT, Unit>(_ => Process.replyIfAsked(message));

    /// <summary>
    /// Reply to an ask with an error
    /// </summary>
    /// <remarks>
    /// This should be used from within a process' message loop only
    /// </remarks>
    public static Eff<RT, Unit> replyError(Exception exception) =>
        Eff<RT, Unit>(_ => Process.replyError(exception));

    /// <summary>
    /// Reply to an ask with an error
    /// </summary>
    /// <remarks>
    /// This should be used from within a process' message loop only
    /// </remarks>
    public static Eff<RT, Unit> replyError(string errorMessage) =>
        replyError(new Exception(errorMessage));

    /// <summary>
    /// Reply with an error if asked
    /// </summary>
    /// <remarks>
    /// This should be used from within a process' message loop only
    /// </remarks>
    public static Eff<RT, Unit> replyErrorIfAsked(Exception exception) =>
        isAsk.Bind(x => x ?  replyError(exception) : unitEff);

    /// <summary>
    /// Reply with an error if asked
    /// </summary>
    /// <remarks>
    /// This should be used from within a process' message loop only
    /// </remarks>
    public static Eff<RT, Unit> replyErrorIfAsked(string errorMessage) =>
        isAsk.Bind(x => x ?  replyError(errorMessage) : unitEff);

    /// <summary>
    /// Reply to the asker, or if it's not an ask then tell the sender
    /// via a message to their inbox.
    /// </summary>
    public static Eff<RT, Unit> replyOrTellSender<A>(A message) =>
        from aks in isAsk
        from res in aks ? reply(message) 
                        : message is IReturn { HasValue: true }
                            ? tell(Sender, message)
                            : unitEff
        select res;
}

