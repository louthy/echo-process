using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Schema;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Parsec;
using static LanguageExt.Prelude;
using static LanguageExt.Parsec.Prim;
using static LanguageExt.Parsec.Char;
using static LanguageExt.Parsec.Expr;
using static LanguageExt.Parsec.Token;
using LanguageExt.UnitsOfMeasure;
using Newtonsoft.Json;

namespace Echo.Config
{
    /// <summary>
    /// Parses and provides access to configuration settings relating
    /// to the role and individual processes.
    /// </summary>
    public class ProcessSystemConfig
    {
        public readonly SystemName SystemName;
        public readonly Option<ClusterToken> Cluster;
        public readonly string NodeName;
        public readonly int MaxMailboxSize;

        internal readonly Time Timeout;
        internal readonly bool TransactionalIO = false;
        internal readonly SettingOverrides SettingOverrides;
        internal readonly Types Types;
        
        readonly HashMap<string, ValueToken> RoleSettings;
        readonly HashMap<ProcessId, ProcessToken> ProcessSettings;
        readonly HashMap<string, State<StrategyContext, Unit>> StratSettings;
        readonly Time SessionTimeoutCheck;
 
        public readonly static ProcessSystemConfig Empty =
            new ProcessSystemConfig(
                default(SystemName),
                "root",
                HashMap<string, ValueToken>(),
                HashMap<ProcessId, ProcessToken>(),
                HashMap<string, State<StrategyContext, Unit>>(),
                null,
                new Types(),
                default,
                30*seconds,     
                60*seconds,
                100000,
                false
            );

        ProcessSystemConfig(
            SystemName systemName,
            string nodeName,
            HashMap<string, ValueToken> roleSettings,
            HashMap<ProcessId, ProcessToken> processSettings,
            HashMap<string, State<StrategyContext, Unit>> stratSettings,
            Option<ClusterToken> cluster,
            Types types,
            SettingOverrides settingOverrides,
            Time timeout,
            Time sessionTimeoutCheck,
            int maxMailboxSize,
            bool transactionalIO
            )
        {
            SystemName = systemName;
            NodeName = nodeName;
            SettingOverrides = default;
            RoleSettings = roleSettings;
            ProcessSettings = processSettings;
            StratSettings = stratSettings;
            Cluster = cluster;
            Types = types;
            SettingOverrides = settingOverrides;
            Timeout = timeout;
            SessionTimeoutCheck = sessionTimeoutCheck;
            MaxMailboxSize = maxMailboxSize;
            TransactionalIO = transactionalIO;
            RoleSettingsKey = $"role-{Role.Current}@settings";
            RoleSettingsMaps = Seq(RoleSettings, Cluster.Map(c => c.Settings).IfNone(HashMap<string, ValueToken>()));
            StatePersistsFlag = cluster.IsSome ? ProcessFlags.PersistState : ProcessFlags.Default;
            ClusterSettingsMaps = Seq1(Cluster.Map(c => c.Settings).IfNone(HashMap<string, ValueToken>()));
        }
        
        /// <summary>
        /// Ctor function
        /// </summary>
        [Pure]
        public static ProcessSystemConfig New(SystemName systemName,
            string nodeName,
            HashMap<string, ValueToken> roleSettings,
            HashMap<ProcessId, ProcessToken> processSettings,
            HashMap<string, State<StrategyContext, Unit>> stratSettings,
            Option<ClusterToken> cluster,
            Types types) =>
            new ProcessSystemConfig(
                systemName, 
                nodeName, 
                roleSettings, 
                processSettings, 
                stratSettings, 
                cluster, 
                types,
                default,
                30*seconds,
                60*seconds,
                100000,
                false);

        /// <summary>
        /// Configuration transformation
        /// </summary>
        [Pure]
        internal ProcessSystemConfig With(
            SystemName? SystemName = null,
            string NodeName = null,
            HashMap<string, ValueToken>? RoleSettings = null,
            HashMap<ProcessId, ProcessToken>? ProcessSettings = null,
            HashMap<string, State<StrategyContext, Unit>>? StratSettings = null,
            Option<ClusterToken>? Cluster = null,
            Types Types = null,
            SettingOverrides? SettingOverrides = null,
            Time? Timeout = null,
            Time? SessionTimeoutCheck = null,
            int? MaxMailboxSize = null,
            bool? TransactionalIO = null) =>            
            new ProcessSystemConfig(
                SystemName ?? this.SystemName, 
                NodeName ?? this.NodeName, 
                RoleSettings ?? this.RoleSettings, 
                ProcessSettings ?? this.ProcessSettings, 
                StratSettings ?? this.StratSettings, 
                Cluster ?? this.Cluster, 
                Types ?? this.Types,
                SettingOverrides ?? this.SettingOverrides,
                Timeout ?? this.Timeout,
                SessionTimeoutCheck ?? this.SessionTimeoutCheck,
                MaxMailboxSize ?? this.MaxMailboxSize,
                TransactionalIO ?? this.TransactionalIO);
        
        /// <summary>
        /// Get the roles settings key
        /// </summary>
        public string RoleSettingsKey { get; }

        /// <summary>
        /// All the settings for a role, in scope order (role -> cluster)
        /// </summary>
        public Seq<HashMap<string, ValueToken>> RoleSettingsMaps { get; }

        /// <summary>
        /// If we have a cluster then this returns ProcessFlags.PersistState else ProcessFlags.Default
        /// </summary>
        public ProcessFlags StatePersistsFlag { get; }

        /// <summary>
        /// All the settings for a cluster
        /// </summary>
        public Seq<HashMap<string, ValueToken>> ClusterSettingsMaps { get; }
        
        [Pure]
        internal ProcessSystemConfig ApplyUpdates(SettingOverrideUpdates updates) =>
            With(SettingOverrides: SettingOverrides.ApplyUpdates(updates));
        
        [Pure]
        internal ProcessSystemConfig ApplyUpdates(params SettingOverrideUpdates[] updates) =>
            With(SettingOverrides: SettingOverrides.ApplyUpdates(SettingOverrideUpdates.Concat(updates)));
 
        /// <summary>
        /// Returns a named strategy
        /// </summary>
        [Pure]
        internal Option<State<StrategyContext, Unit>> GetStrategy(string name) =>
            StratSettings.Find(name);

        [Pure]
        internal Option<HashMap<string, (object Value, long Timestamp)>> GetProcessSettingsOverrides(ProcessId pid) =>
            SettingOverrides.Find(pid.Path);

        /// <summary>
        /// Make a process ID into a /role/... ID
        /// </summary>
        [Pure]
        static ProcessId RolePid(ProcessId pid) =>
            ProcessId.Top["role"].Append(pid.Skip(1));

        /// <summary>
        /// All the settings for a process, in scope order (local -> local-role -> role -> cluster)
        /// </summary>
        [Pure]
        internal Seq<HashMap<string, ValueToken>> SettingsMaps(ProcessId pid) =>
            Seq(
                ProcessSettings.Find(pid).Map(token => token.Settings).IfNone(HashMap<string, ValueToken>()),
                ProcessSettings.Find(RolePid(pid)).Map(token => token.Settings).IfNone(HashMap<string, ValueToken>()),
                RoleSettings,
                Cluster.Map(c => c.Settings).IfNone(HashMap<string, ValueToken>()));


        /// <summary>
        /// This is the setting for how often sessions are checked for expiry, *not*
        /// the expiry time itself.  That is set on each sessionStart()
        /// </summary>
        [Pure]
        internal Time SessionTimeoutCheckFrequency =>
            SessionTimeoutCheck;
    
        [Pure]
        internal static Option<ValueToken> mapTokenType<A>(ValueToken token, ProcessSystemConfig psc) =>
            token.Value is A avalue
                ? new ValueToken(token.Type, avalue)
                : from def in psc.Types.TypeMap.Find(typeof(A).FullName)
                  from map in def.Convert(token)
                  select map;
    }

        
    internal struct SettingOverride
    {
        public enum Tag
        {
            DeleteKey,
            DeletePropKey,
            Update
        }

        public readonly string Key;
        public readonly string PropKey;
        public readonly object Value;
        public readonly Tag Type;
        public readonly long Timestamp;
    
        SettingOverride(string key, string propKey, object value)
        {
            Key = key;
            PropKey = propKey;
            Value = value;
            Type = Tag.Update;                   
            Timestamp = DateTime.UtcNow.Ticks;
        }
    
        SettingOverride(string key, string propKey)
        {
            Key = key;
            PropKey = propKey;
            Value = default;
            Type = Tag.DeletePropKey;                   
            Timestamp = DateTime.UtcNow.Ticks;
        }
    
        SettingOverride(string key)
        {
            Key = key;
            PropKey = default;
            Value = default;
            Type = Tag.DeleteKey;                   
            Timestamp = DateTime.UtcNow.Ticks;
        }

        [Pure]
        public static SettingOverride CreateOrUpdate(string key, string propKey, object value) =>
            new SettingOverride(key, propKey, value);

        [Pure]
        public static SettingOverride Delete(string key, string propKey) =>
            new SettingOverride(key, propKey);

        [Pure]
        public static SettingOverride Delete(string key) =>
            new SettingOverride(key);
    }

    internal struct SettingOverrideUpdates
    {
        public static SettingOverrideUpdates Empty = default;
        public readonly Seq<SettingOverride> Updates;
        
        public SettingOverrideUpdates(Seq<SettingOverride> updates) =>
            Updates = updates;

        [Pure]
        public SettingOverrideUpdates Add(SettingOverride update) =>
            new SettingOverrideUpdates(Updates.Add(update));

        [Pure]
        public SettingOverrideUpdates Append(SettingOverrideUpdates updates) =>
            new SettingOverrideUpdates(Updates + updates.Updates);

        [Pure]
        public static SettingOverrideUpdates Concat(Seq<SettingOverrideUpdates> xs) =>
            xs.IsEmpty
                ? SettingOverrideUpdates.Empty
                : new SettingOverrideUpdates(xs.Head.Updates + Concat(xs.Tail).Updates);

        [Pure]
        public static SettingOverrideUpdates Concat(params SettingOverrideUpdates[] xs) =>
            Concat(Seq(xs));
    }

    internal struct SettingOverrides
    {
        public readonly HashMap<string, HashMap<string, (object Value, long Timestamp)>> Settings;

        /// <summary>
        /// Ctor
        /// </summary>
        SettingOverrides(HashMap<string, HashMap<string, (object Value, long Timestamp)>> settings) =>
            Settings = settings;

        [Pure]
        public Option<HashMap<string, (object Value, long Timestamp)>> Find(string key) =>
            Settings.Find(key);

        [Pure]
        public Option<(object Value, long Timestamp)> Find(string key, string propKey) =>
            Settings.Find(key, propKey);

        [Pure]
        public Option<object> FindValue(string key, string propKey) =>
            Settings.Find(key, propKey).Map(x => x.Value);

        [Pure]
        public Option<A> FindValue<A>(string key, string propKey) =>
            Settings.Find(key, propKey).Map(x => x.Value).Map(x => (A)x);
        
        /// <summary>
        /// Apply the updates
        /// </summary>
        [Pure]
        public SettingOverrides ApplyUpdates(SettingOverrideUpdates updates)
        {
            var target = Settings;
            foreach (var update in updates.Updates.OrderBy(u => u.Timestamp))
            {
                target = ApplyUpdate(target, update);
            }
            return new SettingOverrides(target);
        }

        [Pure]
        public SettingOverrides ApplyUpdates(SettingOverride update) =>
            new SettingOverrides(ApplyUpdate(Settings, update));
 
        static HashMap<string, HashMap<string, (object Value, long Timestamp)>> ApplyUpdate(
            HashMap<string, HashMap<string, (object Value, long Timestamp)>> target, 
            SettingOverride update
            )
        {
            target = update.Type switch
            {
                // Update a setting if the new timestamp is greater than the current setting
                // If there is no current setting, add the new update
                SettingOverride.Tag.Update =>
                target.Find(update.Key, update.PropKey)
                    .Filter(s => s.Timestamp < update.Timestamp)
                    .Map(s => target.AddOrUpdate(update.Key, update.PropKey, (update.Value, update.Timestamp)))
                    .IfNone(() => target.AddOrUpdate(update.Key, update.PropKey, (update.Value, update.Timestamp))),

                // Remove a setting if the new timestamp is greater than the current setting
                SettingOverride.Tag.DeletePropKey =>
                target.Find(update.Key, update.PropKey)
                    .Filter(s => s.Timestamp < update.Timestamp)
                    .Map(s => target.Remove(update.Key, update.PropKey))
                    .IfNone(target),

                // Remove a whole key
                SettingOverride.Tag.DeleteKey =>
                target.Find(update.Key)
                    .Map(s => target.Remove(update.Key))
                    .IfNone(target),

                _ => throw new NotSupportedException()
            };
            return target;
        }
    }
}
