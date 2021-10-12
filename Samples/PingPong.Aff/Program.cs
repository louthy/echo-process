using Echo;
using System;
using D = System.Diagnostics;
using Echo.Traits;
using LanguageExt;
using LanguageExt.Sys;
using static Echo.Process<Echo.Runtime>;
using static LanguageExt.Prelude;
using static LanguageExt.Sys.Console<Echo.Runtime>;

const int interval = 100000;  

// Simple logging of errors
Process.ProcessSystemLog.Subscribe(System.Console.WriteLine);

// Initialise the process system
ProcessConfig.initialise();

// Create a runtime for the Aff system
var runtime = Runtime.New(EchoState<Runtime>.Default);


// Launch three processes

var application = from logger in spawn<D.Stopwatch, string>("logger", startWatch(), loggerInbox)
                  from ping   in spawn<int>("ping", pingInbox, Shutdown: shutdownInbox)
                  from pong   in spawn<int>("pong", pongInbox, Shutdown: shutdownInbox)
                  
                  from _1     in tell(ping, 0, pong)
                  
                  from _2     in readKey
                  from _3     in writeLine("Shutting down...")
                  
                  from _4     in shutdownAll
                  
                  from _5     in writeLine("Goodbye!")
                  
                  select unit;

await application.Run(runtime);

// Ping process inbox 
Aff<Runtime, Unit> pingInbox(int n) =>
    from lg in User.Map(u => u.Child("logger"))
    from _1 in when(n % interval == 0, tell(lg, $"{n}"))
    from _2 in tell(Sender, n + 1)
    select unit;

// Pong process inbox 
Aff<Runtime, Unit> pongInbox(int n) =>
    tell(Sender, n + 1);

// Logger process inbox
static Aff<Runtime, D.Stopwatch> loggerInbox(D.Stopwatch sw, string message) =>
    from _1 in stopWatch(sw)
    from _2 in writeLine($"{message}: duration = {sw.ElapsedMilliseconds}ms")
    from _3 in restartWatch(sw)
    select sw;

// Write to the console on shutdown
static Aff<Runtime, Unit> shutdownInbox() =>
    writeLine($"shutdown: {Process.Self}");

// Simple wrapper of the Stopwatch Start
static Eff<D.Stopwatch> startWatch() =>
    Eff(() => {
            var sw = new D.Stopwatch();
            sw.Start();
            return sw;
        });

// Simple wrapper of the Stopwatch Stop
static Eff<Unit> stopWatch(D.Stopwatch sw) =>
    Eff(() => {
            sw.Stop();
            return unit;
        });
    
// Simple wrapper of the Stopwatch Restart
static Eff<Unit> restartWatch(D.Stopwatch sw) =>
    Eff(() => { 
            sw.Restart(); 
            return unit; 
        });