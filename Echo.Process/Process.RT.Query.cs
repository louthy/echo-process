using Echo.Traits;
using LanguageExt;
using System.Collections.Generic;
using LanguageExt.Effects.Traits;
using static LanguageExt.Prelude;

namespace Echo
{
    public partial class Process<RT>
        where RT : struct, HasCancel<RT>, HasEcho<RT>
    {
        /// <summary>
        /// Finds all *persistent* registered names in a role
        /// </summary>
        /// <param name="role">Role to limit search to</param>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Registered names</returns>
        public static Aff<RT, IEnumerable<ProcessName>> queryRegistered(ProcessName role, string keyQuery) =>
            CurrentSystem.Map(sn => Process.queryRegistered(role, keyQuery, sn));

        /// <summary>
        /// Finds all *persistent* processes based on the search pattern provided.  Note the returned
        /// ProcessIds may contain processes that aren't currently active.  You can still post
        /// to them however.
        /// </summary>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Matching ProcessIds</returns>
        public static Aff<RT, IEnumerable<ProcessId>> queryProcesses(string keyQuery) =>
            CurrentSystem.Map(sn => Process.queryProcesses(keyQuery, sn));

        /// <summary>
        /// Finds all *persistent* processes based on the search pattern provided and then returns the
        /// meta-data associated with them.
        /// </summary>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Map of ProcessId to ProcessMetaData</returns>
        public static Aff<RT, HashMap<ProcessId, ProcessMetaData>> queryProcessMetaData(string keyQuery) =>
            CurrentSystem.Map(sn => Process.queryProcessMetaData(keyQuery, sn));
    }
}
