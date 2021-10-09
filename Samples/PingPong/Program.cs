using Echo;
using System;
using static System.Console;
using static Echo.Process;

var ping = ProcessId.None;
var pong = ProcessId.None;

Process.ProcessSystemLog.Subscribe(WriteLine);

ProcessConfig.initialise();

ping = spawn<int>("ping",
                  m => 
                  {
                      if(m % 10000 == 0) WriteLine($"ping {m}");
                      tell(pong, m + 1);
                  });

pong = spawn<int>("pong",
                  m => 
                  {
                      //WriteLine($"pong {m}");
                      tell(ping, m + 1);
                  });

tell(ping, 0);

ReadKey();