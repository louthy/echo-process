using Echo;
using System;
using System.Diagnostics;
using LanguageExt;
using static System.Console;
using static Echo.Process;
using Process = Echo.Process;

const int interval = 100000;  

Process.ProcessSystemLog.Subscribe(WriteLine);
ProcessConfig.initialise();

var logger = spawn<Stopwatch, string>("logger", loggerSetup, loggerInbox);
var ping   = spawn<int>("ping", pingInbox, Shutdown: shutdownInbox);
var pong   = spawn<int>("pong", pongInbox, Shutdown: shutdownInbox);

tell(ping, 0, pong);

ReadKey();

WriteLine("Shutting down...");
shutdownAll();
WriteLine("Goodbye!");

void pingInbox(int n)
{
    if (n % interval == 0) tell(logger, $"{n}");
    tell(Sender, n + 1);
}

void pongInbox(int n) =>
    tell(Sender, n + 1);

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

