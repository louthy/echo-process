using LanguageExt;
using System;
using Echo.Traits;
using LanguageExt.Effects.Traits;
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
    public static partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
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
        public static Aff<RT, Unit> reply<T>(T message) =>
            Eff(() => Process.reply(message));

        /// <summary>
        /// Reply if asked
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> replyIfAsked<T>(T message) =>
            Eff(() => Process.replyIfAsked(message));

        /// <summary>
        /// Reply to an ask with an error
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> replyError(Exception exception) =>
            Eff(() => Process.replyError(exception));

        /// <summary>
        /// Reply to an ask with an error
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> replyError(string errorMessage) =>
            Eff(() => Process.replyError(errorMessage));

        /// <summary>
        /// Reply with an error if asked
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> replyErrorIfAsked(Exception exception) =>
            Eff(() => Process.replyErrorIfAsked(exception));

        /// <summary>
        /// Reply with an error if asked
        /// </summary>
        /// <remarks>
        /// This should be used from within a process' message loop only
        /// </remarks>
        public static Aff<RT, Unit> replyErrorIfAsked(string errorMessage) =>
            Eff(() => Process.replyErrorIfAsked(errorMessage));

        /// <summary>
        /// Reply to the asker, or if it's not an ask then tell the sender
        /// via a message to their inbox.
        /// </summary>
        public static Aff<RT, Unit> replyOrTellSender<T>(T message) =>
            Eff(() => Process.replyOrTellSender(message));
    }
}
