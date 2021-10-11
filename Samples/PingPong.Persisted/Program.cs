using Echo;
using System;
using System.Diagnostics;
using LanguageExt;
using LanguageExt.Pipes;
using static System.Console;
using static Echo.Process;
using Process = Echo.Process;

const int interval = 1000;

RedisCluster.register();
Process.ProcessSystemLog.Subscribe(WriteLine);
ProcessConfig.initialise("app", "examples", "ping-pong", "localhost", "5", "redis");

var logger = spawn<Stopwatch, string>("logger", loggerSetup, loggerInbox);
var ping   = spawn<int>("ping", pingInbox, ProcessFlags.PersistInbox, Shutdown: shutdownInbox);
var pong   = spawn<int>("pong", pongInbox, ProcessFlags.PersistInbox, Shutdown: shutdownInbox);

tell(ping, 0);

ReadKey();
WriteLine("Killing ping pong processes...");

kill(ping);
kill(pong);

WriteLine("Done, press enter to shutdown");
ReadKey();

shutdownAll();

WriteLine("Goodbye!");

void pingInbox(int n)
{
    if (n % interval == 0) tell(logger, $"{n}");
    tell(pong, n + 1);
}

void pongInbox(int n)
{
    tell(ping, n + 1);
}

static Stopwatch loggerSetup()
{
    var sw = new Stopwatch();
    sw.Start();
    return sw;
}

static Stopwatch loggerInbox(Stopwatch sw, string message)
{
    sw.Stop();
    WriteLine($"{message}: duration = {sw.ElapsedMilliseconds}ms");
    sw.Restart();
    return sw;
}

static Unit shutdownInbox()
{
    Console.WriteLine($"shutdown: {Process.Self}");
    return default;
}

