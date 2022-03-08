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
        public static Aff<RT, Unit> dead(object message, string reason, SystemName system = default(SystemName)) =>
            Eff(() => Process.dead(message, reason, system));

        /// <summary>
        /// Forward a message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="message">Dead letter message</param>
        /// <param name="ex">Exception that caused the dead-letter</param>
        public static Aff<RT, Unit> dead(object message, Exception ex, SystemName system = default(SystemName)) =>
            Eff(() => Process.dead(message, ex, system));

        /// <summary>
        /// Forward a message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="message">Dead letter message</param>
        /// <param name="ex">Exception that caused the dead-letter</param>
        /// <param name="reason">Reason for the dead-letter</param>
        public static Aff<RT, Unit> dead(object message, Exception ex, string reason, SystemName system = default(SystemName)) =>
            Eff(() => Process.dead(message, ex, reason, system));

        /// <summary>
        /// Forward the current message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="reason">Reason for the dead-letter</param>
        public static Aff<RT, Unit> dead(string reason, SystemName system = default(SystemName)) =>
            Eff(() => Process.dead(reason, system));

        /// <summary>
        /// Forward the current message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="ex">Exception that caused the dead-letter</param>
        public static Aff<RT, Unit> dead(Exception ex, SystemName system = default(SystemName)) =>
            Eff(() => Process.dead(ex, system));

        /// <summary>
        /// Forward a message to dead-letters (and wrap it in a contextual dead-letter
        /// structure)
        /// </summary>
        /// <param name="ex">Exception that caused the dead-letter</param>
        /// <param name="reason">Reason for the dead-letter</param>
        public static Aff<RT, Unit> dead(Exception ex, string reason, SystemName system = default(SystemName)) =>
            Eff(() => Process.dead(ex, reason, system));
    }
}
