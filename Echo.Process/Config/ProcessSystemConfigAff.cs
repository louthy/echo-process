using System;
using System.Diagnostics.Contracts;
using System.Linq;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using LanguageExt.UnitsOfMeasure;
using LanguageExt.UnsafeValueAccess;

namespace Echo.Config
{
    internal static class ProcessSystemConfigAff<RT> where RT : struct, HasEcho<RT> 
    {
        /// <summary>
        /// Write a single override setting
        /// </summary>
        [Pure]
        public static Aff<RT, Unit> writeSettingOverride(string key, object value, string name, string prop, ProcessFlags flags) =>
            from _       in value == null
                                ? FailEff<Unit>(Error.New("Settings can't be null"))
                                : unitEff
            from propKey in SuccessEff($"{name}@{prop}")
            from success in flags.HasPersistence()
                                ? Echo.Cluster.hashFieldAddOrUpdate<RT, object>(key, propKey, value)
                                : unitEff
            from sys     in ActorContextAff<RT>.LocalSystem
            from __      in sys.ApplyUpdate(SettingOverride.CreateOrUpdate(key, propKey, value))
            select unit;
        
        /// <summary>
        /// Clear a single override setting
        /// </summary>
        [Pure]
        public static Aff<RT, Unit> clearSettingOverride(string key, string name, string prop, ProcessFlags flags) =>
            from propKey in SuccessEff($"{name}@{prop}")
            from success in flags.HasPersistence()
                                ? Echo.Cluster.deleteHashField<RT>(key, propKey)
                                : SuccessEff(true)
            from sys     in ActorContextAff<RT>.LocalSystem
            from __      in sys.ApplyUpdate(SettingOverride.Delete(key, propKey))
            select unit;

        /// <summary>
        /// Clear all override settings for either the process or role
        /// </summary>
        [Pure]
        public static Aff<RT, Unit> clearSettingsOverride(string key, ProcessFlags flags) =>
            from success in flags.HasPersistence()
                                ? Echo.Cluster.delete<RT>(key)
                                : SuccessEff(true)
            from sys     in ActorContextAff<RT>.LocalSystem
            from __      in sys.ApplyUpdate(SettingOverride.Delete(key))
            select unit;

        /// <summary>
        /// Clear all override settings for either the process or role
        /// </summary>
        [Pure]
        public static Eff<RT, Unit> clearInMemorySettingsOverride(string key) =>
            from sys     in ActorContextAff<RT>.LocalSystem
            from __      in sys.ApplyUpdate(SettingOverride.Delete(key))
            select unit;

        /// <summary>
        /// All the settings for a process, in scope order (local -> local-role -> role -> cluster)
        /// </summary>
        [Pure]
        static Eff<RT, Seq<HashMap<string, ValueToken>>> settingsMaps(ProcessId pid) =>
            from psc in ActorContextAff<RT>.LocalSettings 
            select psc.SettingsMaps(pid);

        /// <summary>
        /// Get the roles settings key
        /// </summary>
        [Pure]
        static Eff<RT, string> RoleSettingsKey =>
            from psc in ActorContextAff<RT>.LocalSettings 
            select psc.RoleSettingsKey;

        /// <summary>
        /// All the settings for a role, in scope order (role -> cluster)
        /// </summary>
        [Pure]
        static Eff<RT, Seq<HashMap<string, ValueToken>>> RoleSettingsMaps =>
            from psc in ActorContextAff<RT>.LocalSettings 
            select psc.RoleSettingsMaps;

        /// <summary>
        /// If we have a cluster then this returns ProcessFlags.PersistState else ProcessFlags.Default
        /// </summary>
        [Pure]
        public static Eff<RT, ProcessFlags> StatePersistsFlag =>
            from psc in ActorContextAff<RT>.LocalSettings 
            select psc.StatePersistsFlag;

        /// <summary>
        /// All the settings for a cluster
        /// </summary>
        [Pure]
        public static Eff<RT, Seq<HashMap<string, ValueToken>>> ClusterSettingsMaps =>
            ActorContextAff<RT>.LocalSettings.Map(psc =>psc.ClusterSettingsMaps);

        /// <summary>
        /// Get a named process setting
        /// </summary>
        /// <param name="pid">Process</param>
        /// <param name="name">Name of setting</param>
        /// <param name="prop">Name of property within the setting (for complex 
        /// types, not value types)</param>
        /// <param name="defaultValue">Value to use if the setting doesn't exist</param>
        /// <returns>Updated config (cache updated) and the setting value</returns>
        [Pure]
        public static Aff<RT, A> getProcessSetting<A>(
            ProcessId pid, 
            string name, 
            string prop, 
            Aff<RT, A> defaultValue) =>
                from empty        in SuccessEff(HashMap<string, ValueToken>())
                from settingsMaps in settingsMaps(pid)
                from result       in getSettingGeneral<A>(settingsMaps, ActorInboxCommon.ClusterSettingsKey(pid), name, prop, defaultValue)
                select result;

        /// <summary>
        /// Get a named process setting
        /// </summary>
        /// <param name="pid">Process</param>
        /// <param name="name">Name of setting</param>
        /// <param name="prop">Name of property within the setting (for complex 
        /// types, not value types)</param>
        /// <returns>Optional setting value</returns>
        [Pure]
        public static Aff<RT, A> getProcessSetting<A>(
            ProcessId pid, 
            string name, 
            string prop) =>
                from empty        in SuccessEff(HashMap<string, ValueToken>())
                from settingsMaps in settingsMaps(pid)
                from result       in getSettingGeneral<A>(settingsMaps, ActorInboxCommon.ClusterSettingsKey(pid), name, prop)
                select result;

        /// <summary>
        /// Get a named role setting
        /// </summary>
        /// <param name="name">Name of setting</param>
        /// <param name="prop">Name of property within the setting (for complex 
        /// types, not value types)</param>
        /// <param name="defaultValue">Value to use if the setting doesn't exist</param>
        /// <returns>Updated config (cache updated) and the setting value</returns>
        [Pure]
        public static Aff<RT, A> getRoleSetting<A>(
            string name, 
            string prop, 
            Aff<RT, A> defaultValue) =>
                from key in RoleSettingsKey
                from sgs in RoleSettingsMaps
                from res in getSettingGeneral<A>(sgs, key, name, prop, defaultValue)
                select res;
        
        /// <summary>
        /// Get a named cluster setting
        /// </summary>
        /// <param name="name">Name of setting</param>
        /// <param name="prop">Name of property within the setting (for complex 
        /// types, not value types)</param>
        /// <param name="defaultValue">Value to use if the setting doesn't exist</param>
        [Pure]
        public static Aff<RT, A> getClusterSetting<A>(
            string name, 
            string prop, 
            Aff<RT, A> defaultValue) =>
                getClusterSetting<A>(name, prop)
                    .BiBind(SuccessEff, _ => defaultValue);

        /// <summary>
        /// Get a named cluster setting
        /// </summary>
        /// <param name="name">Name of setting</param>
        /// <param name="prop">Name of property within the setting (for complex 
        /// types, not value types)</param>
        [Pure]
        public static Aff<RT, A> getClusterSetting<A>(
            string name, 
            string prop) => 
                from psc in ActorContextAff<RT>.LocalSettings 
                from sgs in ClusterSettingsMaps
                from res in getSettingGeneral<A>(sgs, "cluster@settings", name, prop)
                select res;

        /// <summary>
        /// Get the flags for a Process.  Returns ProcessFlags.Default if none
        /// have been set in the config.
        /// </summary>
        [Pure]
        public static Aff<RT, ProcessFlags> getProcessFlags(ProcessId pid) =>
            getProcessSetting<ProcessFlags>(pid, "flags", "value", SuccessEff(ProcessFlags.Default));

        /// <summary>
        /// Get the strategy for a Process.  Returns Process.DefaultStrategy if one
        /// hasn't been set in the config.
        /// </summary>
        public static Aff<RT, State<StrategyContext, Unit>> getProcessStrategy(ProcessId pid) =>
            getProcessSetting<State<StrategyContext, Unit>>(pid, "strategy", "value", SuccessEff(Process.DefaultStrategy));
        
        [Pure]
        public static Aff<RT, A> getSettingGeneral<A>(
            Seq<HashMap<string, ValueToken>> settingsMaps, 
            string key, 
            string name, 
            string prop, 
            Aff<RT, A> defaultValue) =>
                getSettingGeneral<A>(settingsMaps, key, name, prop)
                    .Match(Succ: SuccessEff, 
                           Fail: e => from dv in defaultValue
                                      from rs in addOrUpdateProcessOverride(key, $"{name}@{prop}", dv)
                                      select rs)
                    .Flatten();

        [Pure]
        static Eff<RT, A> addOrUpdateProcessOverride<A>(string key, string propKey, A value) =>
            from sys in ActorContextAff<RT>.LocalSystem
            from ___ in sys.ApplyUpdate(SettingOverride.CreateOrUpdate(key, propKey, value))
            select value;

        [Pure]
        public static Aff<RT, A> getSettingGeneral<A>(
            Seq<HashMap<string, ValueToken>> settingsMaps, 
            string key, 
            string name, 
            string prop) =>
                from psc     in ActorContextAff<RT>.LocalSettings
                from propKey in SuccessAff($"{name}@{prop}")
                from result  in psc.SettingOverrides
                                   .FindValue<A>(key, propKey)
                                   .Match(
                                       Some: SuccessEff,
                                       None: retreiveSettingGeneral<A>(settingsMaps, key, name, prop))
                select result;
                             
        [Pure]
        static Aff<RT, A> retreiveSettingGeneral<A>(
            Seq<HashMap<string, ValueToken>> settingsMaps, 
            string key, 
            string name, 
            string prop) =>
                from psc    in ActorContextAff<RT>.LocalSettings
                from flags  in StatePersistsFlag
                from result in flags.HasPersistence() 
                                    ? retreivePersistentSettingGeneral<A>(settingsMaps, key, name, prop)
                                    : retreiveSettingGeneralFromMaps<A>(settingsMaps, key, name, prop)
                select result;
        
        [Pure]
        static Aff<RT, A> retreivePersistentSettingGeneral<A>(
            Seq<HashMap<string, ValueToken>> settingsMaps, 
            string key, 
            string name, 
            string prop) =>
                from propKey in SuccessAff($"{name}@{prop}")
                from tover   in Cluster.getHashField<RT, A>(key, propKey)
                from result  in tover.Match(
                                    Some: x  => addOrUpdateProcessOverride(key, propKey, x),
                                    None: retreiveSettingGeneralFromMaps<A>(settingsMaps, key, name, prop))
                select result;

        [Pure]
        static Eff<RT, A> retreiveSettingGeneralFromMaps<A>(
            Seq<HashMap<string, ValueToken>> settingsMaps,            
            string key, 
            string name, 
            string prop) =>
                settingsMaps.IsEmpty
                    ? FailEff<A>(Error.New($"Setting '{key}: {name}@{prop}' doesn't exist"))
                    : from psc      in ActorContextAff<RT>.LocalSettings
                      from propKey  in SuccessEff($"{name}@{prop}")
                      from settings in SuccessEff(settingsMaps.Head)
                      from tover    in SuccessEff(from opt1 in prop == "value"
                                                                  ? from tok in settings.Find(name)  
                                                                    from map in ProcessSystemConfig.mapTokenType<A>(tok, psc).Map(v => (A)v.Value)
                                                                    select map
                                                                  : settings.Find(name).Map(v => v.GetItem<A>(prop))
                                                  from opt2 in opt1
                                                  select opt2)
                      from result   in tover.IsSome
                                           ? addOrUpdateProcessOverride(key, propKey, tover.ValueUnsafe())
                                           : retreiveSettingGeneralFromMaps<A>(settingsMaps.Tail, key, name, prop)
                      select result;

        [Pure]
        public static Aff<RT, Unit> postConnect =>
            from mmb in getRoleSetting<int>("mailbox-size", "value", SuccessEff(100000))
            from tim in getRoleSetting<Time>("timeout", "value", SuccessEff(30 * seconds))
            from ses in getRoleSetting<Time>("session-timeout-check", "value", SuccessEff(60 * seconds))
            from tra in getRoleSetting<bool>("transactional-io", "value", SuccessEff(true))
            from sys in ActorContextAff<RT>.LocalSystem
            from sgs in sys.Settings.SwapEff(s => SuccessEff(s.With(MaxMailboxSize:      mmb,
                                                                    Timeout:             tim,
                                                                    SessionTimeoutCheck: ses,
                                                                    TransactionalIO:     tra)))
            select unit;

        /// <summary>
        /// Get the name to use to register the Process
        /// </summary>
        [Pure]
        public static Aff<RT, ProcessName> getProcessRegisteredName(ProcessId pid) =>
            getProcessSetting<ProcessName>(pid, "register-as", "value");        
    }
}