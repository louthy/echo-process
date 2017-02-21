# echo-process
Actor system that works alongside the functional framework [Language-Ext](https://github.com/louthy/language-ext)

An issue with working with C# is that no matter how much of [Language-Ext functional framework](https://github.com/louthy/language-ext) you take on-board, you will always end up bumping into mutable state or side-effecting systems.  A way around that is to package up the side-effects into atoms of functional computation that are attached to the mutable state (in whatever form it may take).  The [Actor model](https://en.wikipedia.org/wiki/Actor_model) + functional message handling expressions are the perfect programming model for that.  

Concurrent programming in C# isn't a huge amount of fun.  Yes the TPL gets you lots of stuff for free, but it doesn't magically protect you from race conditions or accessing shared state, and definitely doesn't help with accessing shared external state like a database.

### Documentation

Documention | Description
------------|------------
[Overview](https://github.com/louthy/echo-process/wiki/Process-system) | A quick guide to the core features of the `Process` system
[`tell`](https://github.com/louthy/echo-process/wiki/Tell) | Send a message to a `Process` - This should be your prefered mechanism for communicating with processes
[`ask`](https://github.com/louthy/echo-process/wiki/Ask) | Request/response for processes - use this sparingly.
[Publish / Subscribe](https://github.com/louthy/echo-process/wiki/Publish) | Mechanism for a Process to publish messages and state.  Other processes can subscribe through their inbox or external systems can subscribe through Reactive streams (Observables).
[Message dispatch](https://github.com/louthy/echo-process/wiki/Process-system-message-dispatch) | The power of any actor system, especially when it comes to a changing network topology is in its message routing and dispatching
[ProcessId](https://github.com/louthy/echo-process/wiki/ProcessId) |  `Process` address/location mechansim
[Routers](https://github.com/louthy/echo-process/wiki/Routers) | A router is a `Process`  that manage sets of 'worker' processes by routing the received messages, following pre-defined behaviours, e.g. Round-robin, broadcast, etc.
[Dispatchers](https://github.com/louthy/echo-process/wiki/Dispatchers) | Similar to routers but without the need for a router process, all routing is done by the sender
[Registered processes](https://github.com/echo-process/language-ext/wiki/Registered-processes) | A sort of DNS for Processes, can also register dispatchers
[Roles](https://github.com/louthy/echo-process/wiki/Roles) | A special type of dispatcher that's aware of the aliveness of cluster nodes and what their roles are

### Getting started

Make sure you have the `Echo.Process` DLL included in your project.  If you're using F# then you will also need to include `Echo.Process.FSharp`.

In C# you should be `using static Echo.Process`, if you're not using C# 6, just prefix all functions in the examples below with `Process.`

If you want to use it with Redis, include `Echo.Process.Redis.dll`.  To connect to Redis use:

```C#
    // C#
    RedisCluster.register();
    ProcessConfig.initialise("sys", "web-front-end", "web-front-end-1", "localhost", "0");
```
* `"sys"` is the 'system name', but easier to think of it as the name of the cluster as a whole.  That means you can use a different value to point it at another Redis db (for multiple clusters).  But for now it's easier to call it `sys` and leave it.
* `"web-front-end"` is the role, you can have multiple nodes in a role and the role based dispatchers allow you to implement high-availability and load balancing strategies.
* `"web-front-end-1"` is the name of this node, and should be unique in the cluster
* `"localhost"` is the Redis connection (can be comma separated for multiple Redis nodes)
* `"0"` is the Redis catalogue to use (`"0" - "15"`)

Note, neither of those lines are needed if you're doing in-app messaging only.

### Nuget

Nu-get package | Description
---------------|-------------
[LanguageExt.Core](https://www.nuget.org/packages/LanguageExt.Core) | All of the core types and functional 'prelude'.  This is the core framework that the Echo library is based upon.
[Echo.Process](https://www.nuget.org/packages/Echo.Process) | 'Erlang like' actor system for in-app messaging and massive concurrency
[Echo.Process.Redis](https://www.nuget.org/packages/Echo.Process.Redis) | Cluster support for the `LangaugeExt.Process` system for cluster aware processes using Redis for queue and state persistence
[Echo.Process.Owin](https://www.nuget.org/packages/Echo.Process.Owin) | WebSockets gateway into the Process system.  Uses Owin to register the WebSockets handler.
[Echo.ProcessJS](https://www.nuget.org/packages/Echo.ProcessJS) | Javascript API to the `Echo.Process` system.  Supports running of Processes in a client browser, with hooks for two-way UI binding
