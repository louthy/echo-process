using System;
using System.Collections.Generic;
using System.IO;
using Echo.Config;
using static LanguageExt.Prelude;
using static Echo.Process;
using LanguageExt;
using OpenTracing;

#if !NETSTANDARD
using System.Web;
#endif

namespace Echo
{
    public static class ProcessConfig
    {
        static readonly object sync = new object();
        internal static ITracer Tracer; 

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
        /// <param name="tracer">Optional support for Open Tracing</param>
        public static Unit initialiseWeb(string nodeName, IEnumerable<FuncSpec> strategyFuncs = null, ITracer tracer = null) =>
            initialiseWeb(nodeName, () => { }, strategyFuncs, tracer: tracer);

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
        /// <param name="appPath">Path to the application where the conf files are</param>
        /// <param name="tracer">Optional support for Open Tracing</param>
        public static Unit initialiseWeb(string nodeName, Action setup, IEnumerable<FuncSpec> strategyFuncs = null, string appPath = null, ITracer tracer = null)
        {
            lock (sync)
            {
                return initialiseFileSystem(nodeName, setup, strategyFuncs, appPath, tracer);
            }
        }

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
        /// <param name="tracer">Optional support for Open Tracing</param>
        public static Unit initialiseFileSystem(string nodeName, IEnumerable<FuncSpec> strategyFuncs = null, ITracer tracer = null)
        {
            lock (sync)
            {
                return initialiseFileSystem(nodeName, () => { }, strategyFuncs, tracer: tracer);
            }
        }

        /// <summary>
        /// Process system configuration initialisation
        /// This will look for process.conf files in the application folder.  It can run multiple times, 
        /// once the config has loaded the system won't re-load the config until you call ProcessConfig.unload() followed 
        /// by  ProcessConfig.initialiseFileSystem(...), so it's safe to not surround it with ifs.
        /// </summary>
        /// <param name="setup">A setup function to call on successful loading of the configuration files - this will
        /// happen once only.</param>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        /// <param name="tracer">Optional support for Open Tracing</param>
        public static Unit initialiseFileSystem(Action setup, IEnumerable<FuncSpec> strategyFuncs = null, ITracer tracer = null)
        {
            lock (sync)
            {
                return initialiseFileSystem(null, setup, strategyFuncs, tracer: tracer);
            }
        }

        /// <summary>
        /// Process system configuration initialisation
        /// This will look for process.conf files in the application folder.  It can run multiple times, 
        /// once the config has loaded the system won't re-load the config until you call ProcessConfig.unload() followed 
        /// by ProcessConfig.initialiseFileSystem(...), so it's safe to not surround it with ifs.
        /// </summary>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        /// <param name="tracer">Optional support for Open Tracing</param>
        public static Unit initialiseFileSystem(IEnumerable<FuncSpec> strategyFuncs = null, ITracer tracer = null)
        {
            lock (sync)
            {
                return initialiseFileSystem(null, () => { }, strategyFuncs, tracer: tracer);
            }
        }

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
        /// <param name="appPath">Path to the application where the conf files are</param>
        /// <param name="tracer">Optional support for Open Tracing</param>
        public static Unit initialiseFileSystem(string nodeName, Action setup, IEnumerable<FuncSpec> strategyFuncs = null, string appPath = null, ITracer tracer = null)
        {
            lock (sync)
            {
                appPath ??= AppDomain.CurrentDomain.BaseDirectory;
                var clusterPath = Path.Combine(appPath, "cluster.conf");
                var processPath = Path.Combine(appPath, "process.conf");

                var clusterText =
                    File.Exists(clusterPath)
                        ? File.ReadAllText(clusterPath)
                        : "";

                var processText = File.Exists(processPath)
                    ? File.ReadAllText(processPath)
                    : "";

                return initialise(clusterText + processText, nodeName, setup, strategyFuncs);
            }
        }

        /// <summary>
        /// Process system initialisation
        /// Initialises am in-memory only Process system
        /// </summary>
        /// <param name="tracer">Optional support for Open Tracing</param>
        public static Unit initialise(ITracer tracer = null) =>
            initialise("", None, () => { }, tracer: tracer);

        /// <summary>
        /// Initialise without a config file or text
        /// </summary>
        /// <param name="systemName">Name of the system - this is most useful</param>
        /// <param name="roleName"></param>
        /// <param name="nodeName"></param>
        /// <param name="providerName"></param>
        /// <param name="connectionString"></param>
        /// <param name="catalogueName"></param>
        /// <param name="tracer">Optional support for Open Tracing</param>
        /// <returns></returns>
        public static Unit initialise(
            SystemName systemName,
            ProcessName roleName,
            ProcessName nodeName,
            string connectionString,
            string catalogueName,
            string providerName = "redis",
            ITracer tracer = null
            )
        {
            lock (sync)
            {
                var types = new Types();
                Tracer = tracer;

                StartFromConfig(new ProcessSystemConfig(
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
                    types
                ));
            }
            return unit;
        }

        /// <summary>
        /// Process system configuration initialisation
        /// This will parse the configuration text, It can run multiple times, once the config has loaded the system won't 
        /// re-load the config until you call ProcessConfig.unload() followed by ProcessConfig.initialise(...), so it's safe 
        /// to not surround it with ifs.
        /// 
        /// NOTE: If a cluster is specified in config text and its 'node-name' matches nodeName, then those settings 
        /// will be used to connect to the cluster.  This allows for different staging environments to be setup.
        /// </summary>
        /// <param name="nodeName">If a cluster is specified in the cluster.conf and its 'node-name' matches nodeName, then 
        /// those settings will be used to connect to the cluster.  This allows for different staging environments to be 
        /// setup.</param>
        /// <param name="setup">A setup function to call on successful loading of the configuration files - this will
        /// happen once only.</param>
        /// <param name="strategyFuncs">Plugin extra strategy behaviours by passing in a list of FuncSpecs.</param>
        /// <param name="tracer">Optional support for Open Tracing</param>
        /// <param name="configText">Configuration source text</param>
        public static Unit initialise(string configText, Option<string> nodeName, Action setup = null, IEnumerable<FuncSpec> strategyFuncs = null, ITracer tracer = null)
        {
            lock (sync)
            {
                Tracer = tracer;
                var parser = new ProcessSystemConfigParser(nodeName.IfNone(""), new Types(), strategyFuncs);
                var configs = String.IsNullOrWhiteSpace(configText)
                    ? HashMap(Tuple(new SystemName(""), ProcessSystemConfig.Empty))
                    : parser.ParseConfigText(configText);

                nodeName.Map(_ => configs.Filter(c => c.NodeName == nodeName).Iter(StartFromConfig))
                        .IfNone(() => configs.Filter(c => c.NodeName == "root").Iter(StartFromConfig));

                setup?.Invoke();
                return unit;
            }
        }

        static void StartFromConfig(ProcessSystemConfig config)
        {
            lock (sync)
            {
                config.Cluster.Match(
                    Some: _ =>
                    {
                        // Extract cluster settings
                        var provider = config.GetClusterSetting("provider", "value", "redis");
                        var role = config.GetClusterSetting("role", "value", name => clusterSettingMissing<string>(name));
                        var clusterConn = config.GetClusterSetting("connection", "value", "localhost");
                        var clusterDb = config.GetClusterSetting("database", "value", "0");
                        var env = config.SystemName;
                        var userEnv = config.GetClusterSetting<string>("user-env", "value");

                        var appProfile = new AppProfile(
                            config.NodeName,
                            role,
                            clusterConn,
                            clusterDb,
                            env,
                            userEnv
                            );

                        // Look for an existing actor-system with the same system name
                        var current = ActorContext.Systems.Filter(c => c.Value == env).HeadOrNone();

                        // Work out if something significant has changed that will cause us to restart
                        var restart = current.Map(ActorContext.System)
                                                 .Map(c => c.AppProfile.NodeName != appProfile.NodeName ||
                                                           c.AppProfile.Role != appProfile.Role ||
                                                           c.AppProfile.ClusterConn != appProfile.ClusterConn ||
                                                           c.AppProfile.ClusterDb != appProfile.ClusterDb);

                        // Either restart / update settings / or start new
                        restart.Match(
                            Some: r =>
                            {
                                if (r)
                                {
                                    // Restart
                                    try
                                    {
                                        ActorContext.StopSystem(env);
                                    }
                                    catch (Exception e)
                                    {
                                        logErr(e);
                                    }
                                    StartFromConfig(config);
                                }
                                else
                                {
                                    // Update settings
                                    ActorContext.System(env).UpdateSettings(config, appProfile);
                                        var cluster = from systm in current.Map(ActorContext.System)
                                                        from clstr in systm.Cluster
                                                        select clstr;
                                }
                            },
                            None: () =>
                            {
                                // Start new
                                ICluster cluster = Cluster.connect(
                                        provider,
                                        config.NodeName,
                                        clusterConn,
                                        clusterDb,
                                        role
                                        );

                                ActorContext.StartSystem(env, Optional(cluster), appProfile, config);
                                config.PostConnect();
                            });
                    },
                    None: () =>
                    {
                        ActorContext.StartSystem(new SystemName(""), None, AppProfile.NonClustered, config);
                    });
            }
        }

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

        static T clusterSettingMissing<T>(string name) =>
            failwith<T>("Cluster setting missing: " + name);

#if !NETSTANDARD
        static string hostName(HttpContext context) =>
            context.Request.Url.Host == "localhost"
                ? System.Environment.MachineName
                : context.Request.Url.Host;
#endif

    }
}
