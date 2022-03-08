using System;
using System.Linq;
using System.Reactive.Linq;
using Echo.Traits;
using static LanguageExt.Prelude;
using static LanguageExt.Map;
using LanguageExt;
using LanguageExt.Effects.Traits;

namespace Echo
{
    public static partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        /// <summary>
        /// Forward a message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="message">Dead letter message</param>
        /// <param name="reason">Reason for the dead-letter</param>
        public static Aff<RT, Unit> dead(object message, string reason) =>
            CurrentSystem.Map(sn => Process.dead(message, reason, sn));

        /// <summary>
        /// Forward a message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="message">Dead letter message</param>
        /// <param name="ex">Exception that caused the dead-letter</param>
        public static Aff<RT, Unit> dead(object message, Exception ex) =>
            CurrentSystem.Map(sn => Process.dead(message, ex, sn));

        /// <summary>
        /// Forward a message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="message">Dead letter message</param>
        /// <param name="ex">Exception that caused the dead-letter</param>
        /// <param name="reason">Reason for the dead-letter</param>
        public static Aff<RT, Unit> dead(object message, Exception ex, string reason) =>
            CurrentSystem.Map(sn => Process.dead(message, ex, reason, sn));

        /// <summary>
        /// Forward the current message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="reason">Reason for the dead-letter</param>
        public static Aff<RT, Unit> dead(string reason) =>
            CurrentSystem.Map(sn => Process.dead(reason, sn));

        /// <summary>
        /// Forward the current message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="ex">Exception that caused the dead-letter</param>
        public static Aff<RT, Unit> dead(Exception ex) =>
            CurrentSystem.Map(sn => Process.dead(ex, sn));

        /// <summary>
        /// Forward a message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="ex">Exception that caused the dead-letter</param>
        /// <param name="reason">Reason for the dead-letter</param>
        public static Aff<RT, Unit> dead(Exception ex, string reason) =>
            CurrentSystem.Map(sn => Process.dead(ex, reason, sn));
    }
}
