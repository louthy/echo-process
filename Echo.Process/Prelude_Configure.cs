using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using System.Windows.Forms;
using Echo.Config;
using static LanguageExt.Prelude;
using static Echo.Process;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
#if !NETSTANDARD
using System.Web;
#endif

namespace Echo
{
    public static class ProcessConfig
    {
        static IDisposable localScheduler;

        static Unit InitLocalScheduler()
        {
            if(localScheduler != null)
            {
                localScheduler.Dispose();
            }
            localScheduler = LocalScheduler.Run();
            return default;
        }

        internal static Unit ShutdownLocalScheduler()
        {
            localScheduler.Dispose();
            localScheduler = null;
            return default;
        }

#if !NETSTANDARD

        /// <summary>
        /// Process system configuration initialisation
        /// This will look for cluster.conf and process.conf files in the web application folder, you should call this
        /// function from within Application_BeginRequest of Global.asax.  It can run multiple times, once the config 
        /// has loaded the system won't re-load the config until you call ProcessConfig.unload() by 
        /// ProcessConfig.initialiseWeb(...)
        /// </summary>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        public static Aff<RT, Unit> initialiseWeb<RT>(IEnumerable<FuncSpec> strategyFuncs = null) where RT : struct, HasEcho<RT> =>
            initialiseWeb<RT>(() => { }, strategyFuncs);

        /// <summary>
        /// Process system configuration initialisation
        /// This will look for cluster.conf and process.conf files in the web application folder, you should call this
        /// function from within Application_BeginRequest of Global.asax.  It can run multiple times, once the config 
        /// has loaded the system won't re-load the config until you call ProcessConfig.unload() followed by 
        /// ProcessConfig.initialiseWeb(...)
        /// 
        /// NOTE: If a cluster is specified in the cluster.conf and its 'node-name' matches the host name of the web-
        /// application (i.e. www.example.com), then those settings will be used to connect to the cluster.  
        /// This allows for different staging environments to be setup.
        /// </summary>
        /// <param name="setup">A setup function to call on successful loading of the configuration files - this will
        /// happen once only.</param>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        public static Aff<RT, Unit> initialiseWeb<RT>(Action setup, IEnumerable<FuncSpec> strategyFuncs = null) where RT : struct, HasEcho<RT>
        {
            if (HttpContext.Current == null) throw new NotSupportedException("There must be a valid HttpContext.Current to call ProcessConfig.initialiseWeb()");
            return initialiseFileSystem<RT>(hostName(HttpContext.Current), setup, strategyFuncs, AppDomain.CurrentDomain.BaseDirectory);
        }
#endif


        /// <summary>
        /// Process system configuration initialisation
        /// This will look for `cluster.conf` and `process.conf` files in the web application folder, you should call this
        /// function from within `Application_BeginRequest` of `Global.asax` or in the `Configuration` function of an OWIN 
        /// `Startup` type.  
        /// </summary>
        /// <param name="nodeName">
        /// <para>Web-site host-name: i.e. `www.example.com` - you would usually call this when you 
        /// have your first Request object:  `HttpContext.Request.Url.Host`.  Could also be the site-
        /// name: `System.Web.Hosting.HostingEnvironment.SiteName`.  Anything that will identify this
        /// node, and allow for multiple staging environments to be configured in the `cluster.conf`</para>
        /// <para>
        ///     i.e.
        /// </para>
        /// <para>
        ///     object sync = new object();
        ///     bool started = false;
        ///     
        ///             static bool started = false;
        ///             static object sync = new object();
        ///             
        ///             protected void Application_BeginRequest(Object sender, EventArgs e)
        ///             {
        ///                 if (!started)
        ///                 {
        ///                     lock (sync)
        ///                     {
        ///                         if (!started)
        ///                         {
        ///                             ProcessConfig.initialiseWeb(HttpContext.Request.Url.Host);
        ///                             started = true;
        ///                         }
        ///                     }
        ///                 }
        ///             }
        /// </para>
        /// </param>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        public static Aff<RT, Unit> initialiseWeb<RT>(string nodeName, IEnumerable<FuncSpec> strategyFuncs = null) where RT : struct, HasEcho<RT> =>
            initialiseWeb<RT>(nodeName, () => { }, strategyFuncs);

        /// <param name="nodeName">
        /// <para>Web-site host-name: i.e. `www.example.com` - you would usually call this when you 
        /// have your first Request object:  `HttpContext.Request.Url.Host`.  Could also be the site-
        /// name: `System.Web.Hosting.HostingEnvironment.SiteName`.  Anything that will identify this
        /// node, and allow for multiple staging environments to be configured in the `cluster.conf`</para>
        /// <para>
        ///     i.e.
        /// </para>
        /// <para>
        ///     object sync = new object();
        ///     bool started = false;
        ///     
        ///             static bool started = false;
        ///             static object sync = new object();
        ///             
        ///             protected void Application_BeginRequest(Object sender, EventArgs e)
        ///             {
        ///                 if (!started)
        ///                 {
        ///                     lock (sync)
        ///                     {
        ///                         if (!started)
        ///                         {
        ///                             ProcessConfig.initialiseWeb(HttpContext.Request.Url.Host);
        ///                             started = true;
        ///                         }
        ///                     }
        ///                 }
        ///             }
        /// </para>
        /// </param>
        /// <param name="setup">A setup function to call on successful loading of the configuration files - this will
        /// happen once only.
        /// <para>
        ///     object sync = new object();
        ///     bool started = false;
        ///     
        ///             static bool started = false;
        ///             static object sync = new object();
        ///             
        ///             protected void Application_BeginRequest(Object sender, EventArgs e)
        ///             {
        ///                 if (!started)
        ///                 {
        ///                     lock (sync)
        ///                     {
        ///                         if (!started)
        ///                         {
        ///                             ProcessConfig.initialiseWeb(HttpContext.Request.Url.Host);
        ///                             started = true;
        ///                         }
        ///                     }
        ///                 }
        ///             }
        /// </para>
        /// </param>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        public static Aff<RT, Unit> initialiseWeb<RT>(string nodeName, Action setup, IEnumerable<FuncSpec> strategyFuncs = null, string appPath = null) where RT : struct, HasEcho<RT> =>
            initialiseFileSystem<RT>(nodeName, setup, strategyFuncs, appPath);

        /// <summary>
        /// Process system configuration initialisation
        /// This will look for cluster.conf and process.conf files in the application folder.  It can run multiple times, 
        /// once the config has loaded the system won't re-load the config until you call ProcessConfig.unload() followed 
        /// by ProcessConfig.initialiseFileSystem(...), so it's safe to not surround it with ifs.
        /// 
        /// NOTE: If a cluster is specified in the cluster.conf and its 'node-name' matches nodeName, then those settings 
        /// will be used to connect to the cluster.  This allows for different staging environments to be setup.
        /// </summary>
        /// <param name="nodeName">If a cluster is specified in the cluster.conf and its 'node-name' matches nodeName, then 
        /// those settings will be used to connect to the cluster.  This allows for different staging environments to be 
        /// setup.</param>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        public static Aff<RT, Unit> initialiseFileSystem<RT>(string nodeName, IEnumerable<FuncSpec> strategyFuncs = null) where RT : struct, HasEcho<RT> =>
            initialiseFileSystem<RT>(nodeName, () => { }, strategyFuncs);

        /// <summary>
        /// Process system configuration initialisation
        /// This will look for process.conf files in the application folder.  It can run multiple times, 
        /// once the config has loaded the system won't re-load the config until you call ProcessConfig.unload() followed 
        /// by  ProcessConfig.initialiseFileSystem(...), so it's safe to not surround it with ifs.
        /// </summary>
        /// <param name="setup">A setup function to call on successful loading of the configuration files - this will
        /// happen once only.</param>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        public static Aff<RT, Unit> initialiseFileSystem<RT>(Action setup, IEnumerable<FuncSpec> strategyFuncs = null) where RT : struct, HasEcho<RT> =>
            initialiseFileSystem<RT>(null, setup, strategyFuncs);

        /// <summary>
        /// Process system configuration initialisation
        /// This will look for process.conf files in the application folder.  It can run multiple times, 
        /// once the config has loaded the system won't re-load the config until you call ProcessConfig.unload() followed 
        /// by ProcessConfig.initialiseFileSystem(...), so it's safe to not surround it with ifs.
        /// </summary>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        public static Aff<RT, Unit> initialiseFileSystem<RT>(IEnumerable<FuncSpec> strategyFuncs = null) where RT : struct, HasEcho<RT> =>
            initialiseFileSystem<RT>(null, () => { }, strategyFuncs);

        /// <summary>
        /// Process system configuration initialisation
        /// This will look for cluster.conf and process.conf files in the application folder.  It can run multiple times, 
        /// but once the config has loaded the system won't re-load the config until you call ProcessConfig.unload() followed 
        /// by  ProcessConfig.initialiseFileSystem(...), so it's safe to not surround it with ifs.
        /// 
        /// NOTE: If a cluster is specified in the cluster.conf and its 'node-name' matches nodeName, then those settings 
        /// will be used to connect to the cluster.  This allows for different staging environments to be setup.
        /// </summary>
        /// <param name="nodeName">If a cluster is specified in the cluster.conf and its 'node-name' matches nodeName, then 
        /// those settings will be used to connect to the cluster.  This allows for different staging environments to be 
        /// setup.</param>
        /// <param name="setup">A setup function to call on successful loading of the configuration files - this will
        /// happen once only.</param>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        public static Aff<RT, Unit> initialiseFileSystem<RT>(string nodeName, Action setup, IEnumerable<FuncSpec> strategyFuncs = null, string applicationPath = null)
            where RT : struct, HasEcho<RT> =>
#if NETSTANDARD
            from appPath in SuccessEff<string>(applicationPath ?? "")
#else
            from appPath in SuccessEff<string>(applicationPath ?? AppDomain.CurrentDomain.BaseDirectory ?? "")
#endif
            from clusterPath in SuccessEff(Path.Combine(appPath, "cluster.conf"))
            from processPath in SuccessEff(Path.Combine(appPath, "process.conf"))
            from clusterText in IO.File.readAllText<RT>(clusterPath)
            from processText in IO.File.readAllText<RT>(processPath)
            from result in initialise<RT>(clusterText + processText, nodeName, setup, strategyFuncs)
            select result;

        /// <summary>
        /// Process system initialisation
        /// Initialises am in-memory only Process system
        /// </summary>
        public static Aff<RT, Unit> initialise<RT>() where RT : struct, HasEcho<RT> =>
            initialise<RT>("", None, () => { });

        /// <summary>
        /// Initialise without a config file or text
        /// </summary>
        /// <param name="systemName">Name of the system - this is most useful</param>
        /// <param name="roleName"></param>
        /// <param name="nodeName"></param>
        /// <param name="providerName"></param>
        /// <param name="connectionString"></param>
        /// <param name="catalogueName"></param>
        /// <returns></returns>
        public static Aff<RT, Unit> initialise<RT>(
            SystemName systemName,
            ProcessName roleName,
            ProcessName nodeName,
            string connectionString,
            string catalogueName,
            string providerName = "redis") 
            where RT : struct, HasEcho<RT> =>
                from types in SuccessEff(new Types())
                from resul in startFromConfig<RT>(ProcessSystemConfig.New(
                    systemName,
                    nodeName.Value,
                    HashMap<string, ValueToken>(),
                    HashMap<ProcessId, ProcessToken>(),
                    HashMap<string, State<StrategyContext, Unit>>(),
                    new ClusterToken(
                        None,
                        List(
                            new NamedValueToken("node-name", new ValueToken(types.String, nodeName.Value), None),
                            new NamedValueToken("role", new ValueToken(types.String, roleName.Value), None),
                            new NamedValueToken("env", new ValueToken(types.String, systemName.Value), None),
                            new NamedValueToken("connection", new ValueToken(types.String, connectionString), None),
                            new NamedValueToken("database", new ValueToken(types.String, catalogueName), None),
                            new NamedValueToken("provider", new ValueToken(types.String, providerName), None))),
                    types))
                select resul;

        /// <summary>
        /// Process system configuration initialisation
        /// This will parse the configuration text, It can run multiple times, once the config has loaded the system won't 
        /// re-load the config until you call ProcessConfig.unload() followed by ProcessConfig.initialise(...), so it's safe 
        /// to not surround it with ifs.
        /// 
        /// NOTE: If a cluster is specified in config text and its 'node-name' matches nodeName, then those settings 
        /// will be used to connect to the cluster.  This allows for different staging environments to be setup.
        /// </summary>
        /// <param name="configText">Config source text</param>
        /// <param name="nodeName">If a cluster is specified in the cluster.conf and its 'node-name' matches nodeName, then 
        /// those settings will be used to connect to the cluster.  This allows for different staging environments to be 
        /// setup.</param>
        /// <param name="setup">A setup function to call on successful loading of the configuration files - this will
        /// happen once only.</param>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        public static Aff<RT, Unit> initialise<RT>(string configText, Option<string> nodeName, Action setup = null, IEnumerable<FuncSpec> strategyFuncs = null) where RT : struct, HasEcho<RT> =>
            from parser   in SuccessEff(new ProcessSystemConfigParser(nodeName.IfNone(""), new Types(), strategyFuncs))
            from configs  in SuccessEff(String.IsNullOrWhiteSpace(configText)
                                           ? HashMap(Tuple(new SystemName(""), ProcessSystemConfig.Empty))
                                           : parser.ParseConfigText(configText))
            from startups in SuccessEff(nodeName.Map(_ => configs.Filter(c => c.NodeName == nodeName).Map(startFromConfig<RT>))
                                                .IfNone(() => configs.Filter(c => c.NodeName == "root").Map(startFromConfig<RT>)))
            from r        in startups.Values.SequenceParallel()
            from _        in setup == null
                                ? unitEff
                                : SuccessEff(fun(setup)())
            select unit;

        static Aff<RT, Unit> startFromConfig<RT>(ProcessSystemConfig config) where RT : struct, HasEcho<RT> =>
            from _ in Eff(InitLocalScheduler)
            from r in config.Cluster.Match(
                Some: t  => startClusterFromConfig<RT>(config),
                None: () => startNonClusterFromConfig<RT>(config))
            select r;

        static Eff<A> clusterSetting<A>(ProcessSystemConfig config, string name, Eff<A> defaultValue)
        {
            foreach (var settings in config.ClusterSettingsMaps)
            {
                var v = settings.Find(name)
                                .Bind(t => ProcessSystemConfig.mapTokenType<A>(t, config))
                                .Map(v => (A)v.Value);
                
                if (v.IsSome) return SuccessEff(v.ValueUnsafe());
            }
            return defaultValue;
        }

        static Aff<RT, Unit> startClusterFromConfig<RT>(ProcessSystemConfig config) 
            where RT : struct, HasEcho<RT> =>
            
            // Extract cluster settings
            from provider    in clusterSetting(config, "provider", SuccessEff("redis"))
            from role        in clusterSetting(config, "role", FailEff<string>(Error.New("Cluster 'role' setting missing")))
            from clusterConn in clusterSetting(config, "connection", SuccessEff("localhost"))
            from clusterDb   in clusterSetting(config, "database", SuccessEff("0"))
            from userEnv     in clusterSetting(config, "user-env", SuccessEff("value"))
            from env         in SuccessEff(config.SystemName)
            
            // Build an appProfile
            from appProfile  in SuccessEff(new AppProfile(config.NodeName, role, clusterConn, clusterDb, env, userEnv))
        
            // Look for an existing actor-system with the same system name
            from current     in ActorContext.findSystem(env).Match(Some, _ => None)
            
            // Work out if something significant has changed that will cause us to restart
            from restart     in SuccessEff(current.Map(c => c.AppProfile.Value.NodeName != appProfile.NodeName ||
                                                            c.AppProfile.Value.Role != appProfile.Role ||
                                                            c.AppProfile.Value.ClusterConn != appProfile.ClusterConn ||
                                                            c.AppProfile.Value.ClusterDb != appProfile.ClusterDb))
                
            // Restart, update, or start-new
            from result      in restart.Match(
                                   True:  () => restartClusterFromConfig<RT>(config, env),
                                   False: () => updateSettings<RT>(config, appProfile, env),
                                   None:  () => startClusterFromConfig<RT>(provider, clusterConn, clusterDb, role, appProfile, config, env))
            select result;

        static Aff<RT, Unit> startClusterFromConfig<RT>(
            string provider, 
            string clusterConn, 
            string clusterDb, 
            string role, 
            AppProfile appProfile,
            ProcessSystemConfig config, 
            SystemName env) 
                where RT : struct, HasEcho<RT> =>
                    from _1 in Cluster.addSystem<RT>(env, new ClusterConfig(config.NodeName, clusterConn, clusterDb, role))
                    from _2 in Cluster.connect<RT>()
                    from _3 in ActorContextAff<RT>.startSystem(env, true, appProfile, config)
                    from _4 in ProcessSystemConfigAff<RT>.postConnect
                    from _5 in updateSettings<RT>(sgs, appProfile, env)
                    select unit;
        
        /// <summary>
        /// Stop a system and restart it with the new config
        /// </summary>
        static Aff<RT, Unit> restartClusterFromConfig<RT>(ProcessSystemConfig config, SystemName env)
            where RT : struct, HasEcho<RT> =>
                from a in ActorContextAff<RT>.stopSystem(env)
                from b in startFromConfig<RT>(config)
                select b;

        /// <summary>
        /// Update the settings of an existing running system
        /// </summary>
        static Aff<RT, Unit> updateSettings<RT>(ProcessSystemConfig config, AppProfile appProfile, SystemName env)
            where RT : struct, HasEcho<RT> =>
                from sys in ActorContext.findSystem(env)
                from res in ActorContextAff<RT>.localSystem(sys, ActorSystemAff<RT>.updateSettings(config, appProfile))
                select res;

        static Aff<RT, Unit> startNonClusterFromConfig<RT>(ProcessSystemConfig config) 
            where RT : struct, HasEcho<RT> =>
            ActorContext.StartSystem<RT>(new SystemName(""), false, AppProfile.NonClustered, config);

        /// <summary>
        /// Access a setting 
        /// If in a Process message loop, then this accesses the configuration settings
        /// for the Process from the the configuration file, or stored in the cluster.
        /// If not in a Process message loop, then this accesses 'global' configuration
        /// settings.  
        /// </summary>
        /// <param name="name">Name of the setting</param>
        /// <param name="prop">If the setting is a complex value (like a map or record), then 
        /// this selects the property of the setting to access</param>
        /// <returns>Optional configuration setting value</returns>
        public static T read<T>(string name, string prop, T defaultValue, SystemName system = default(SystemName)) =>
            InMessageLoop
                ? ActorContext.System(Self).Settings.GetProcessSetting(Self, name, prop, defaultValue, ActorContext.Request.ProcessFlags)
                : ActorContext.System(system).Settings.GetRoleSetting(name, prop, defaultValue);

        /// <summary>
        /// Access a setting 
        /// If in a Process message loop, then this accesses the configuration settings
        /// for the Process from the the configuration file, or stored in the cluster.
        /// If not in a Process message loop, then this accesses 'global' configuration
        /// settings.  
        /// </summary>
        /// <param name="name">Name of the setting</param>
        /// <param name="prop">If the setting is a complex value (like a map or record), then 
        /// this selects the property of the setting to access</param>
        /// <returns>Optional configuration setting value</returns>
        public static T read<T>(string name, T defaultValue) =>
            read(name, "value", defaultValue);

        /// <summary>
        /// Access a list setting 
        /// If in a Process message loop, then this accesses the configuration settings
        /// for the Process from the the configuration file, or stored in the cluster.
        /// If not in a Process message loop, then this accesses 'global' configuration
        /// settings.  
        /// </summary>
        /// <param name="name">Name of the setting</param>
        /// <param name="prop">If the setting is a complex value (like a map or record), then 
        /// this selects the property of the setting to access</param>
        /// <returns>Optional configuration setting value</returns>
        public static Lst<T> readList<T>(string name, string prop = "value") =>
            read(name, prop, List<T>());

        /// <summary>
        /// Access a map setting 
        /// If in a Process message loop, then this accesses the configuration settings
        /// for the Process from the the configuration file, or stored in the cluster.
        /// If not in a Process message loop, then this accesses 'global' configuration
        /// settings.  
        /// </summary>
        /// <param name="name">Name of the setting</param>
        /// <param name="prop">If the setting is a complex value (like a map or record), then 
        /// this selects the property of the setting to access</param>
        /// <returns>Optional configuration setting value</returns>
        public static HashMap<string, T> readMap<T>(string name, string prop = "value") =>
            read(name, prop, HashMap<string, T>());

        /// <summary>
        /// Write a setting
        /// </summary>
        /// <param name="name">Name of the setting</param>
        /// <param name="value">Value to set</param>
        public static Unit write(string name, object value) =>
            write(name, "value", value);

        /// <summary>
        /// Write a setting
        /// </summary>
        /// <param name="name">Name of the setting</param>
        /// <param name="prop">If the setting is a complex value (like a map or record), then 
        /// this selects the property of the setting to access</param>
        /// <param name="value">Value to set</param>
        public static Unit write(string name, string prop, object value, SystemName system = default(SystemName))
        {
            if (InMessageLoop)
            {
                ActorContext.System(Self)
                            .Settings
                            .WriteSettingOverride(ActorInboxCommon.ClusterSettingsKey(Self), value, name, prop, ActorContext.Request.ProcessFlags);
                return unit;
            }
            else
            {
                return ActorContext.System(system).Settings.WriteSettingOverride($"role-{Role.Current.Value}@settings", value, name, prop, ProcessFlags.PersistState);
            }
        }

        /// <summary>
        /// Clear a setting
        /// </summary>
        /// <param name="name">Name of the setting</param>
        /// <param name="prop">If the setting is a complex value (like a map or record), then 
        /// this selects the property of the setting to access</param>
        public static Unit clear(string name, string prop, SystemName system = default(SystemName))
        {
            if (InMessageLoop)
            {
                ActorContext.System(Self)
                            .Settings
                            .ClearSettingOverride(ActorInboxCommon.ClusterSettingsKey(Self), name, prop, ActorContext.Request.ProcessFlags);
                return unit;
            }
            else
            {
                return ActorContext.System(system).Settings.ClearSettingOverride($"role-{Role.Current.Value}@settings", name, prop, ProcessFlags.PersistState);
            }
        }

        /// <summary>
        /// Clear all settings for the process (or role if outside of the message-loop of a Process)
        /// </summary>
        public static Unit clear(SystemName system = default(SystemName))
        {
            if (InMessageLoop)
            {
                ActorContext.System(Self)
                            .Settings
                            .ClearSettingsOverride(ActorInboxCommon.ClusterSettingsKey(Self), ActorContext.Request.ProcessFlags);
                return unit;
            }
            else
            {
                return ActorContext.System(system).Settings.ClearSettingsOverride($"role-{Role.Current.Value}@settings", ProcessFlags.PersistState);
            }
        }

#if !NETSTANDARD
        static string hostName(HttpContext context) =>
            context.Request.Url.Host == "localhost"
                ? System.Environment.MachineName
                : context.Request.Url.Host;
#endif

    }
}
